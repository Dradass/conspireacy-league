using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Channels;
using ConspiracyLeague.Core.Protocol;

namespace ConspiracyLeague.Core.Net;

/// <summary>
/// Соединение клиента с сервером по WebSocket. Само переподключается после обрыва (с растущей паузой), пока его
/// не остановят (<see cref="Stop"/>). События приходят не сразу, а когда их забирают (<see cref="Poll"/>) — в потоке
/// того, кто забирает (например, в главном потоке игры).
/// </summary>
/// <remarks>
/// По wss:// сертификат сервера проверяется как обычно (доверенный центр, имя сервера). Для своего сервера
/// с самоподписанным сертификатом можно вместо этого задать его отпечаток SHA-256 — тогда принимается только этот
/// сертификат. По ws:// соединение не шифруется.
/// </remarks>
public sealed class WebSocketLink : IDisposable
{
  private static readonly TimeSpan[] Backoff =
  [
    TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8),
  ];

  private readonly Uri _uri;
  private readonly byte[]? _pinnedCertificate;
  private readonly ConcurrentQueue<Action> _events = new();
  private readonly CancellationTokenSource _stop = new();
  private Channel<string> _outgoing = Channel.CreateUnbounded<string>();
  private Task? _loop;

  /// <param name="uri">Адрес сервера: ws://… или wss://….</param>
  /// <param name="certificateSha256">Отпечаток SHA-256 сертификата сервера (hex), если он самоподписанный.</param>
  public WebSocketLink(Uri uri, string? certificateSha256 = null)
  {
    ArgumentNullException.ThrowIfNull(uri);
    if (uri.Scheme is not ("ws" or "wss"))
      throw new ArgumentException("Адрес сервера должен начинаться с ws:// или wss://.", nameof(uri));
    _uri = uri;
    if (!string.IsNullOrWhiteSpace(certificateSha256))
    {
      var hex = certificateSha256.Replace(":", string.Empty).Replace(" ", string.Empty);
      try
      {
        _pinnedCertificate = Convert.FromHexString(hex);
      }
      catch (FormatException error)
      {
        throw new ArgumentException("Отпечаток сертификата — 64 шестнадцатеричные цифры.", nameof(certificateSha256), error);
      }
      if (_pinnedCertificate.Length != SHA256.HashSizeInBytes)
        throw new ArgumentException("Отпечаток сертификата — 64 шестнадцатеричные цифры.", nameof(certificateSha256));
    }
  }

  /// <summary>
  /// Соединение установлено (в том числе после обрыва).
  /// </summary>
  public event Action? Connected;

  /// <summary>
  /// Пришло сообщение.
  /// </summary>
  public event Action<string>? Received;

  /// <summary>
  /// Соединение потеряно или не удалось; аргумент — почему. Если связь не остановлена, она восстановится сама.
  /// </summary>
  public event Action<string>? Disconnected;

  /// <summary>
  /// Соединение сейчас установлено.
  /// </summary>
  public bool IsConnected { get; private set; }

  /// <summary>
  /// Подключиться (и переподключаться после обрывов).
  /// </summary>
  public void Start() => _loop ??= Task.Run(RunAsync);

  /// <summary>
  /// Отправить сообщение. Без связи оно пропадёт — после переподключения клиент сам повторит, что нужно.
  /// </summary>
  public void Send(string json) => _outgoing.Writer.TryWrite(json);

  /// <summary>
  /// Забрать события и вызвать их обработчики в текущем потоке.
  /// </summary>
  public void Poll()
  {
    while (_events.TryDequeue(out var action))
      action();
  }

  /// <summary>
  /// Остановить связь: больше не переподключаться.
  /// </summary>
  public void Stop()
  {
    if (!_stop.IsCancellationRequested)
      _stop.Cancel();
  }

  public void Dispose()
  {
    Stop();
    try
    {
      _loop?.Wait(TimeSpan.FromSeconds(3));
    }
    catch (AggregateException)
    {
    }
  }

  private async Task RunAsync()
  {
    var attempt = 0;
    while (!_stop.IsCancellationRequested)
    {
      string reason;
      try
      {
        using var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        if (_pinnedCertificate is { } pinned)
          socket.Options.RemoteCertificateValidationCallback = (_, certificate, _, _) => Matches(certificate, pinned);
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
        {
          timeout.CancelAfter(TimeSpan.FromSeconds(10));
          await socket.ConnectAsync(_uri, timeout.Token);
        }

        attempt = 0;
        // Сообщения, не отправленные до обрыва, устарели: клиент повторит нужное сам.
        _outgoing = Channel.CreateUnbounded<string>();
        IsConnected = true;
        _events.Enqueue(() => Connected?.Invoke());
        reason = await RunConnectionAsync(socket);
      }
      catch (Exception error) when (error is WebSocketException or OperationCanceledException or IOException
        or System.Net.Sockets.SocketException or System.Security.Authentication.AuthenticationException)
      {
        reason = _stop.IsCancellationRequested ? "Связь остановлена." : $"Нет связи с сервером: {error.Message}";
      }

      if (IsConnected || attempt == 0)
      {
        var text = reason;
        _events.Enqueue(() => Disconnected?.Invoke(text));
      }

      IsConnected = false;
      if (_stop.IsCancellationRequested)
        return;

      try
      {
        await Task.Delay(Backoff[Math.Min(attempt++, Backoff.Length - 1)], _stop.Token);
      }
      catch (OperationCanceledException)
      {
        return;
      }
    }
  }

  private async Task<string> RunConnectionAsync(ClientWebSocket socket)
  {
    using var connection = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
    var outgoing = _outgoing;
    var writer = Task.Run(async () =>
    {
      try
      {
        await foreach (var json in outgoing.Reader.ReadAllAsync(connection.Token))
          await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, connection.Token);
      }
      catch (Exception error) when (error is WebSocketException or OperationCanceledException or ObjectDisposedException or IOException)
      {
      }
    });

    try
    {
      return await ReadAsync(socket, connection.Token);
    }
    finally
    {
      connection.Cancel();
      outgoing.Writer.TryComplete();
      await writer;
      if (_stop.IsCancellationRequested && socket.State == WebSocketState.Open)
      {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
          await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, timeout.Token);
        }
        catch (Exception error) when (error is WebSocketException or OperationCanceledException or IOException)
        {
        }
      }
    }
  }

  private async Task<string> ReadAsync(ClientWebSocket socket, CancellationToken cancellation)
  {
    var limit = ProtocolSerializer.MaxServerMessageBytes;
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
            return "Сервер прислал слишком длинное сообщение.";
          result = await socket.ReceiveAsync(buffer.AsMemory(length, limit + 1 - length), cancellation);
          if (result.MessageType == WebSocketMessageType.Close)
            return socket.CloseStatusDescription is { Length: > 0 } description ? description : "Сервер закрыл соединение.";
          length += result.Count;
        }
        while (!result.EndOfMessage);

        if (result.MessageType != WebSocketMessageType.Text || length > limit)
          return "Сервер прислал неверное сообщение.";
        var text = Encoding.UTF8.GetString(buffer, 0, length);
        _events.Enqueue(() => Received?.Invoke(text));
      }

      return "Соединение закрыто.";
    }
    finally
    {
      ArrayPool<byte>.Shared.Return(buffer);
    }
  }

  private static bool Matches(X509Certificate? certificate, byte[] pinned) =>
    certificate is not null && CryptographicOperations.FixedTimeEquals(SHA256.HashData(certificate.GetRawCertData()), pinned);

  /// <summary>
  /// Отпечаток SHA-256 сертификата (hex) — его сообщают игрокам, чтобы они подключались к своему серверу
  /// с самоподписанным сертификатом.
  /// </summary>
  public static string Fingerprint(X509Certificate2 certificate) => Convert.ToHexString(SHA256.HashData(certificate.RawData));
}
