using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Channels;
using ConspiracyLeague.Core.Protocol;
using ConspiracyLeague.Core.Server;

namespace ConspiracyLeague.Core.Net;

/// <summary>
/// Настройки сетевого сервера.
/// </summary>
public sealed record WebSocketServerOptions
{
  /// <summary>
  /// Сертификат с закрытым ключом для TLS (wss://). Без него соединения не шифруются (ws://) — годится только
  /// для локальной сети.
  /// </summary>
  public X509Certificate2? Certificate { get; init; }

  /// <summary>
  /// Больше соединений с одного адреса сервер не принимает.
  /// </summary>
  public int MaxConnectionsPerAddress { get; init; } = 8;

  /// <summary>
  /// За столько клиент должен пройти TLS и HTTP-рукопожатие.
  /// </summary>
  public TimeSpan HandshakeTimeout { get; init; } = TimeSpan.FromSeconds(10);

  /// <summary>
  /// Больше стольких неотправленных сообщений клиенту — он не успевает их принимать, соединение закрывается.
  /// </summary>
  public int MaxQueuedMessages { get; init; } = 512;

  /// <summary>
  /// Настройки стола.
  /// </summary>
  public TableServerOptions Table { get; init; } = new();

  /// <summary>
  /// Если партия уже шла, а за столом столько времени никого нет, стол накрывается заново — сервер готов к новой партии.
  /// <c>null</c> — стол один на всё время работы сервера.
  /// </summary>
  public TimeSpan? RecycleWhenEmptyAfter { get; init; }
}

/// <summary>
/// Сетевой сервер стола: принимает WebSocket-соединения (по TLS, если задан сертификат) и передаёт сообщения
/// серверу стола (<see cref="TableServer"/>). Все вызовы стола идут из одного потока — по очереди.
/// </summary>
/// <remarks>
/// Защита на уровне транспорта: ограничены размер HTTP-заголовков и время рукопожатия, размер сообщения
/// (<see cref="ProtocolSerializer.MaxClientMessageBytes"/>), число соединений всего и с одного адреса, очередь
/// исходящих сообщений. Принимаются только текстовые сообщения в правильном UTF-8.
/// </remarks>
public sealed class WebSocketTableServer : IAsyncDisposable
{
  private const int MaxHeaderBytes = 8192;
  private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
  private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(100);

  private readonly WebSocketServerOptions _options;
  private readonly TcpListener _listener;
  private readonly CancellationTokenSource _stop = new();
  private readonly Channel<Action> _actions = Channel.CreateUnbounded<Action>(new UnboundedChannelOptions { SingleReader = true });
  private readonly ConcurrentDictionary<IPAddress, int> _perAddress = new();
  private readonly List<Task> _tasks = [];
  private DateTime? _emptySince;

  private WebSocketTableServer(IPEndPoint endpoint, WebSocketServerOptions options, TableServer table)
  {
    _options = options;
    Table = table;
    _listener = new TcpListener(endpoint);
  }

  /// <summary>
  /// Сервер стола. Обращаться к нему можно только через <see cref="Post"/>.
  /// </summary>
  public TableServer Table { get; private set; }

  /// <summary>
  /// Порт, на котором сервер слушает (полезно, если запрошен порт 0 — любой свободный).
  /// </summary>
  public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

  /// <summary>
  /// Шифруются ли соединения.
  /// </summary>
  public bool UsesTls => _options.Certificate is not null;

  /// <summary>
  /// Что произошло — для журнала. Вызывается из разных потоков.
  /// </summary>
  public event Action<string>? Log;

  /// <summary>
  /// Запустить сервер.
  /// </summary>
  /// <param name="endpoint">Адрес и порт.</param>
  /// <param name="options">Настройки.</param>
  public static WebSocketTableServer Start(IPEndPoint endpoint, WebSocketServerOptions? options = null)
  {
    options ??= new WebSocketServerOptions();
    if (options.Certificate is { HasPrivateKey: false })
      throw new ArgumentException("У сертификата нет закрытого ключа.", nameof(options));

    var server = new WebSocketTableServer(endpoint, options, null!);
    server.Table = server.NewTable();
    server._listener.Start();
    server._tasks.Add(Task.Run(server.RunActionsAsync));
    server._tasks.Add(Task.Run(server.AcceptAsync));
    server._tasks.Add(Task.Run(server.TickAsync));
    return server;
  }

  /// <summary>
  /// Выполнить действие со столом в его потоке.
  /// </summary>
  public void Post(Action<TableServer> action) => _actions.Writer.TryWrite(() => action(Table));

  private TableServer NewTable()
  {
    var table = new TableServer(_options.Table);
    table.Log += text => Log?.Invoke(text);
    return table;
  }

  /// <summary>
  /// Партия была, а за столом давно никого — накрыть стол заново (<see cref="WebSocketServerOptions.RecycleWhenEmptyAfter"/>).
  /// Вызывается в потоке стола.
  /// </summary>
  private void RecycleIfEmpty()
  {
    if (_options.RecycleWhenEmptyAfter is not { } after || Table.Game is null || Table.ConnectionCount > 0)
    {
      _emptySince = null;
      return;
    }

    _emptySince ??= DateTime.UtcNow;
    if (DateTime.UtcNow - _emptySince < after)
      return;

    _emptySince = null;
    Table.Dispose();
    Table = NewTable();
    Log?.Invoke("За столом никого — стол готов к новой партии.");
  }

  public async ValueTask DisposeAsync()
  {
    if (_stop.IsCancellationRequested)
      return;

    _stop.Cancel();
    _listener.Stop();
    _actions.Writer.TryComplete();
    try
    {
      await Task.WhenAll(_tasks).WaitAsync(TimeSpan.FromSeconds(5));
    }
    catch (Exception error) when (error is OperationCanceledException or TimeoutException or ObjectDisposedException or SocketException)
    {
    }

    Table.Dispose();
    _stop.Dispose();
  }

  private async Task RunActionsAsync()
  {
    await foreach (var action in _actions.Reader.ReadAllAsync())
    {
      try
      {
        action();
      }
      catch (Exception error)
      {
        // Ошибка в обработке одного сообщения не должна останавливать сервер.
        Log?.Invoke($"Ошибка сервера: {error}");
      }
    }
  }

  private async Task TickAsync()
  {
    using var timer = new PeriodicTimer(TickInterval);
    try
    {
      while (await timer.WaitForNextTickAsync(_stop.Token))
        Post(table =>
        {
          table.Tick();
          RecycleIfEmpty();
        });
    }
    catch (OperationCanceledException)
    {
    }
  }

  private async Task AcceptAsync()
  {
    while (!_stop.IsCancellationRequested)
    {
      TcpClient client;
      try
      {
        client = await _listener.AcceptTcpClientAsync(_stop.Token);
      }
      catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException or SocketException)
      {
        if (_stop.IsCancellationRequested)
          return;
        continue;
      }

      _ = Task.Run(() => HandleAsync(client));
    }
  }

  private async Task HandleAsync(TcpClient client)
  {
    var address = (client.Client.RemoteEndPoint as IPEndPoint)?.Address ?? IPAddress.None;
    if (_perAddress.AddOrUpdate(address, 1, (_, count) => count + 1) > _options.MaxConnectionsPerAddress)
    {
      Release(address);
      client.Dispose();
      return;
    }

    try
    {
      using (client)
      {
        client.NoDelay = true;
        var socket = await AcceptWebSocketAsync(client);
        if (socket is null)
          return;

        using (socket)
          await RunConnectionAsync(socket);
      }
    }
    catch (Exception error) when (error is IOException or SocketException or WebSocketException or AuthenticationException
      or OperationCanceledException or ObjectDisposedException or InvalidOperationException)
    {
      // Обрыв связи или неверный клиент — обычное дело в сети.
    }
    finally
    {
      Release(address);
    }
  }

  private void Release(IPAddress address)
  {
    if (_perAddress.AddOrUpdate(address, 0, (_, count) => count - 1) <= 0)
      _perAddress.TryRemove(new KeyValuePair<IPAddress, int>(address, 0));
  }

  /// <summary>
  /// TLS (если есть сертификат) и HTTP-рукопожатие WebSocket.
  /// </summary>
  /// <returns>WebSocket или <c>null</c>, если клиент не прошёл рукопожатие.</returns>
  private async Task<WebSocket?> AcceptWebSocketAsync(TcpClient client)
  {
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
    timeout.CancelAfter(_options.HandshakeTimeout);
    Stream stream = client.GetStream();
    if (_options.Certificate is { } certificate)
    {
      var ssl = new SslStream(stream, leaveInnerStreamOpen: false);
      await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
      {
        ServerCertificate = certificate,
        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        ClientCertificateRequired = false,
      }, timeout.Token);
      stream = ssl;
    }

    var key = await ReadUpgradeRequestAsync(stream, timeout.Token);
    if (key is null)
    {
      await stream.WriteAsync("HTTP/1.1 400 Bad Request\r\nConnection: close\r\nContent-Length: 0\r\n\r\n"u8.ToArray(), timeout.Token);
      return null;
    }

    var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + WebSocketGuid)));
    var response = $"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n";
    await stream.WriteAsync(Encoding.ASCII.GetBytes(response), timeout.Token);
    return WebSocket.CreateFromStream(stream, new WebSocketCreationOptions { IsServer = true, KeepAliveInterval = TimeSpan.FromSeconds(15) });
  }

  /// <summary>
  /// Прочитать HTTP-запрос на переход к WebSocket (по байту — чтобы не прочитать лишнего).
  /// </summary>
  /// <returns>Ключ Sec-WebSocket-Key или <c>null</c>, если запрос неверный.</returns>
  private static async Task<string?> ReadUpgradeRequestAsync(Stream stream, CancellationToken cancellation)
  {
    var buffer = new byte[MaxHeaderBytes];
    var length = 0;
    while (true)
    {
      if (length == buffer.Length)
        return null;
      var read = await stream.ReadAsync(buffer.AsMemory(length, 1), cancellation);
      if (read == 0)
        return null;
      length++;
      if (length >= 4 && buffer[length - 4] == '\r' && buffer[length - 3] == '\n' && buffer[length - 2] == '\r' && buffer[length - 1] == '\n')
        break;
    }

    var lines = Encoding.ASCII.GetString(buffer, 0, length).Split("\r\n");
    var request = lines[0].Split(' ');
    if (request.Length != 3 || request[0] != "GET" || request[2] != "HTTP/1.1")
      return null;

    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var line in lines.Skip(1).Where(line => line.Length > 0))
    {
      var colon = line.IndexOf(':');
      if (colon <= 0)
        return null;
      headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
    }

    static bool Has(Dictionary<string, string> headers, string name, string token) =>
      headers.TryGetValue(name, out var value)
      && value.Split(',').Any(part => part.Trim().Equals(token, StringComparison.OrdinalIgnoreCase));

    if (!Has(headers, "Upgrade", "websocket") || !Has(headers, "Connection", "upgrade")
      || !headers.TryGetValue("Sec-WebSocket-Version", out var version) || version != "13"
      || !headers.TryGetValue("Sec-WebSocket-Key", out var key))
      return null;

    try
    {
      return Convert.FromBase64String(key).Length == 16 ? key : null;
    }
    catch (FormatException)
    {
      return null;
    }
  }

  private async Task RunConnectionAsync(WebSocket socket)
  {
    using var closing = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
    var connection = new Connection(socket, _options.MaxQueuedMessages, closing);
    var accepted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    Post(table => accepted.TrySetResult(table.Connect(connection)));
    if (!await accepted.Task.WaitAsync(closing.Token))
    {
      await CloseAsync(socket, WebSocketCloseStatus.PolicyViolation, "Сервер переполнен.");
      return;
    }

    var writer = Task.Run(() => connection.WriteAsync());
    try
    {
      await ReadAsync(socket, connection, closing.Token);
    }
    finally
    {
      Post(table => table.Disconnect(connection));
      connection.Complete();
      await writer;
    }
  }

  private async Task ReadAsync(WebSocket socket, Connection connection, CancellationToken cancellation)
  {
    var limit = ProtocolSerializer.MaxClientMessageBytes;
    var buffer = ArrayPool<byte>.Shared.Rent(limit + 1);
    try
    {
      while (socket.State == WebSocketState.Open)
      {
        var length = 0;
        ValueWebSocketReceiveResult result;
        do
        {
          if (length > limit)
          {
            await CloseAndDrainAsync(socket, WebSocketCloseStatus.MessageTooBig, "Слишком длинное сообщение.");
            return;
          }

          result = await socket.ReceiveAsync(buffer.AsMemory(length, limit + 1 - length), cancellation);
          if (result.MessageType == WebSocketMessageType.Close)
            return;
          if (result.MessageType != WebSocketMessageType.Text)
          {
            await CloseAndDrainAsync(socket, WebSocketCloseStatus.InvalidMessageType, "Только текстовые сообщения.");
            return;
          }

          length += result.Count;
        }
        while (!result.EndOfMessage);

        if (length > limit)
        {
          await CloseAndDrainAsync(socket, WebSocketCloseStatus.MessageTooBig, "Слишком длинное сообщение.");
          return;
        }

        string text;
        try
        {
          text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(buffer, 0, length);
        }
        catch (DecoderFallbackException)
        {
          await CloseAndDrainAsync(socket, WebSocketCloseStatus.InvalidPayloadData, "Неверный UTF-8.");
          return;
        }

        Post(table => table.Receive(connection, text));
      }
    }
    finally
    {
      ArrayPool<byte>.Shared.Return(buffer);
    }
  }

  /// <summary>
  /// Закрыть из-за нарушения и дождаться ответа клиента (не дольше пары секунд), отбрасывая то, что он ещё присылает.
  /// Иначе непрочитанные данные заставили бы систему оборвать соединение, и клиент не узнал бы причину.
  /// </summary>
  private static async Task CloseAndDrainAsync(WebSocket socket, WebSocketCloseStatus status, string reason)
  {
    await CloseAsync(socket, status, reason);
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
    var scratch = new byte[4096];
    try
    {
      while (socket.State == WebSocketState.CloseSent)
      {
        var result = await socket.ReceiveAsync(scratch.AsMemory(), timeout.Token);
        if (result.MessageType == WebSocketMessageType.Close)
          break;
      }
    }
    catch (Exception error) when (error is WebSocketException or OperationCanceledException or ObjectDisposedException or IOException)
    {
    }
  }

  private static async Task CloseAsync(WebSocket socket, WebSocketCloseStatus status, string reason)
  {
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
    try
    {
      await socket.CloseOutputAsync(status, reason, timeout.Token);
    }
    catch (Exception error) when (error is WebSocketException or OperationCanceledException or ObjectDisposedException or IOException)
    {
    }
  }

  /// <summary>
  /// Соединение для стола: исходящие сообщения — в очередь, их по порядку отправляет отдельная задача.
  /// </summary>
  private sealed class Connection(WebSocket socket, int maxQueued, CancellationTokenSource closing) : IConnection
  {
    private readonly Channel<string?> _outgoing = Channel.CreateBounded<string?>(new BoundedChannelOptions(maxQueued + 1)
    {
      SingleReader = true,
      FullMode = BoundedChannelFullMode.DropWrite,
    });

    private int _queued;

    public void Send(string json)
    {
      // Клиент не успевает принимать сообщения — закрываем, а не копим память.
      if (Interlocked.Increment(ref _queued) > maxQueued || !_outgoing.Writer.TryWrite(json))
        Close();
    }

    /// <summary>
    /// Закрыть после того, как уйдёт всё, что уже в очереди (например, прощание <see cref="Disconnecting"/>).
    /// </summary>
    public void Close()
    {
      _outgoing.Writer.TryWrite(null);
      _outgoing.Writer.TryComplete();
    }

    public void Complete() => _outgoing.Writer.TryComplete();

    public async Task WriteAsync()
    {
      try
      {
        await foreach (var json in _outgoing.Reader.ReadAllAsync(closing.Token))
        {
          Interlocked.Decrement(ref _queued);
          if (json is null)
            break;
          await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, closing.Token);
        }

        await CloseAsync(socket, WebSocketCloseStatus.NormalClosure, string.Empty);
      }
      catch (Exception error) when (error is WebSocketException or OperationCanceledException or ObjectDisposedException or IOException)
      {
      }
      finally
      {
        // Чтение тоже прекращаем: соединение закрыто.
        try
        {
          closing.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
      }
    }
  }
}
