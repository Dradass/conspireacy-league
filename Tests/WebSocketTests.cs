using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using ConspiracyLeague.Core.Bots;
using ConspiracyLeague.Core.Client;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Net;
using ConspiracyLeague.Core.Server;

namespace ConspiracyLeague.Core.Tests;

[NonParallelizable]
public sealed class WebSocketTests
{
  private static readonly WebSocketServerOptions Options = new()
  {
    Table = new TableServerOptions { BotDelay = TimeSpan.FromMilliseconds(5) },
  };

  private sealed class RemotePlayer : IDisposable
  {
    public RemotePlayer(Uri uri, string name, string? fingerprint = null, string? token = null)
    {
      Link = new WebSocketLink(uri, fingerprint);
      Client = new TableClient(Link.Send, name, token);
      Link.Connected += Client.OnConnected;
      Link.Received += Client.Receive;
      Link.Disconnected += reason => Disconnects.Add(reason);
      Bot = new MirrorBot(Client.Game);
      Link.Start();
    }

    public WebSocketLink Link { get; }

    public TableClient Client { get; }

    public MirrorBot Bot { get; }

    public List<string> Disconnects { get; } = [];

    public void Dispose() => Link.Dispose();
  }

  private static void WaitFor(Func<bool> condition, TimeSpan timeout, params RemotePlayer[] players)
  {
    var watch = Stopwatch.StartNew();
    while (!condition())
    {
      Assert.That(watch.Elapsed, Is.LessThan(timeout), "Не дождались.");
      foreach (var player in players)
        player.Link.Poll();
      Thread.Sleep(2);
    }
  }

  /// <summary>
  /// Партия целиком по настоящей сети (WebSocket через loopback): два человека и бот.
  /// </summary>
  [Test]
  public async Task WholeGameOverWebSocket()
  {
    await using var server = WebSocketTableServer.Start(new IPEndPoint(IPAddress.Loopback, 0), Options);
    var uri = new Uri($"ws://127.0.0.1:{server.Port}/");
    using var host = new RemotePlayer(uri, "Аня");
    WaitFor(() => host.Client.Lobby is not null, TimeSpan.FromSeconds(10), host);
    using var guest = new RemotePlayer(uri, "Боря");
    WaitFor(() => guest.Client.Lobby is not null && host.Client.Lobby!.Seats.Count == 2, TimeSpan.FromSeconds(10), host, guest);

    host.Client.AddBot(BotLevel.Hard);
    WaitFor(() => host.Client.Lobby!.Seats.Count == 3, TimeSpan.FromSeconds(10), host, guest);
    host.Client.Start(GameMode.Money, true);
    WaitFor(() => host.Client.Game.Mirror is not null && guest.Client.Game.Mirror is not null, TimeSpan.FromSeconds(10), host, guest);

    WaitFor(() =>
    {
      host.Bot.Act();
      guest.Bot.Act();
      return host.Client.Game.Mirror!.View.Phase == GamePhase.GameOver && guest.Client.Game.Mirror!.View.Phase == GamePhase.GameOver;
    }, TimeSpan.FromMinutes(3), host, guest);

    Assert.Multiple(() =>
    {
      Assert.That(host.Bot.Rejections + guest.Bot.Rejections, Is.Zero);
      Assert.That(host.Client.Seat, Is.EqualTo(0));
      Assert.That(guest.Client.Seat, Is.EqualTo(1));
      Assert.That(host.Disconnects, Is.Empty);
    });
  }

  /// <summary>
  /// Обрыв настоящего соединения посреди партии: клиент переподключается с токеном и возвращается на своё место.
  /// </summary>
  [Test]
  public async Task ReconnectsAfterConnectionLoss()
  {
    await using var server = WebSocketTableServer.Start(new IPEndPoint(IPAddress.Loopback, 0), Options);
    var uri = new Uri($"ws://127.0.0.1:{server.Port}/");
    using var host = new RemotePlayer(uri, "Аня");
    WaitFor(() => host.Client.Lobby is not null, TimeSpan.FromSeconds(10), host);
    host.Client.AddBot(BotLevel.Easy);
    WaitFor(() => host.Client.Lobby!.Seats.Count == 2, TimeSpan.FromSeconds(10), host);
    host.Client.Start(GameMode.Money, true);
    WaitFor(() => host.Client.Game.Mirror is not null, TimeSpan.FromSeconds(10), host);
    var token = host.Client.Token;

    // Связь пропала: прежнее соединение закрыто, новое — с тем же токеном.
    host.Link.Dispose();
    using var back = new RemotePlayer(uri, "Аня", token: token);
    WaitFor(() => back.Client.Game.Mirror is not null, TimeSpan.FromSeconds(10), back);

    Assert.That(back.Client.Seat, Is.EqualTo(0));
    Assert.That(back.Client.Lobby!.Seats.Count, Is.EqualTo(2));
  }

  private static X509Certificate2 SelfSigned()
  {
    using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256);
    using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    // На Windows TLS-серверу нужен ключ, сохранённый не только в памяти: переимпортируем из PFX.
    return new X509Certificate2(certificate.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
  }

  /// <summary>
  /// TLS: клиент с отпечатком сертификата сервера подключается; с чужим отпечатком — нет (подмена сервера не пройдёт).
  /// </summary>
  [Test]
  public async Task Tls_WithPinnedCertificate()
  {
    using var certificate = SelfSigned();
    await using var server = WebSocketTableServer.Start(new IPEndPoint(IPAddress.Loopback, 0), Options with { Certificate = certificate });
    var uri = new Uri($"wss://127.0.0.1:{server.Port}/");

    using var trusted = new RemotePlayer(uri, "Аня", WebSocketLink.Fingerprint(certificate));
    WaitFor(() => trusted.Client.Lobby is not null, TimeSpan.FromSeconds(10), trusted);

    using var fooled = new RemotePlayer(uri, "Вор", new string('A', 64));
    WaitFor(() => fooled.Disconnects.Count > 0, TimeSpan.FromSeconds(10), fooled);
    Assert.That(fooled.Client.Lobby, Is.Null);

    // Без отпечатка самоподписанный сертификат не принимается.
    using var unpinned = new RemotePlayer(uri, "Гость");
    WaitFor(() => unpinned.Disconnects.Count > 0, TimeSpan.FromSeconds(10), unpinned);
    Assert.That(unpinned.Client.Lobby, Is.Null);
  }

  [Test]
  public async Task OversizedMessage_ClosesConnection()
  {
    await using var server = WebSocketTableServer.Start(new IPEndPoint(IPAddress.Loopback, 0), Options);
    using var socket = new ClientWebSocket();
    await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{server.Port}/"), CancellationToken.None);
    await socket.SendAsync(Encoding.UTF8.GetBytes(new string('x', 100_000)), WebSocketMessageType.Text, true, CancellationToken.None);

    var buffer = new byte[1024];
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var result = await socket.ReceiveAsync(buffer, timeout.Token);

    Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Close));
    Assert.That(socket.CloseStatus, Is.EqualTo(WebSocketCloseStatus.MessageTooBig));
  }

  [Test]
  public async Task PlainHttp_GetsBadRequest()
  {
    await using var server = WebSocketTableServer.Start(new IPEndPoint(IPAddress.Loopback, 0), Options);
    using var client = new TcpClient();
    await client.ConnectAsync(IPAddress.Loopback, server.Port);
    var stream = client.GetStream();
    await stream.WriteAsync("GET / HTTP/1.1\r\nHost: x\r\n\r\n"u8.ToArray());

    var buffer = new byte[256];
    var read = await stream.ReadAsync(buffer).AsTask().WaitAsync(TimeSpan.FromSeconds(10));

    Assert.That(Encoding.ASCII.GetString(buffer, 0, read), Does.StartWith("HTTP/1.1 400"));
  }

  [Test]
  public async Task TooManyConnectionsFromOneAddress_AreRefused()
  {
    await using var server = WebSocketTableServer.Start(new IPEndPoint(IPAddress.Loopback, 0), Options with { MaxConnectionsPerAddress = 2 });
    var uri = new Uri($"ws://127.0.0.1:{server.Port}/");
    var sockets = new List<ClientWebSocket>();
    try
    {
      for (var i = 0; i < 2; i++)
      {
        var socket = new ClientWebSocket();
        await socket.ConnectAsync(uri, CancellationToken.None);
        sockets.Add(socket);
      }

      using var third = new ClientWebSocket();
      Assert.That(async () => await third.ConnectAsync(uri, new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token),
        Throws.InstanceOf<WebSocketException>());
    }
    finally
    {
      foreach (var socket in sockets)
        socket.Dispose();
    }
  }

  private static Task<int> ConnectionCount(WebSocketTableServer server)
  {
    var count = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
    server.Post(table => count.TrySetResult(table.ConnectionCount));
    return count.Task;
  }

  /// <summary>
  /// «Медленные» соединения: открыли TCP и по байту тянут HTTP-заголовки. Они не мешают настоящему игроку сесть за стол
  /// и закрываются сервером по таймауту рукопожатия.
  /// </summary>
  [Test]
  public async Task SlowHandshakes_DoNotBlockPlayersAndTimeOut()
  {
    await using var server = WebSocketTableServer.Start(new IPEndPoint(IPAddress.Loopback, 0),
      Options with { HandshakeTimeout = TimeSpan.FromSeconds(1), MaxConnectionsPerAddress = 100 });
    var slow = new List<TcpClient>();
    try
    {
      for (var i = 0; i < 30; i++)
      {
        var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.Port);
        await client.GetStream().WriteAsync("GET / HTTP/1.1\r\nHost: x\r\n"u8.ToArray());
        slow.Add(client);
      }

      using var player = new RemotePlayer(new Uri($"ws://127.0.0.1:{server.Port}/"), "Аня");
      WaitFor(() => player.Client.Lobby is not null, TimeSpan.FromSeconds(5), player);

      // Через таймаут сервер закрывает все недоговорившие соединения: чтение возвращает 0 или ошибку.
      foreach (var client in slow)
      {
        var buffer = new byte[64];
        int read;
        try
        {
          read = await client.GetStream().ReadAsync(buffer).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (IOException)
        {
          read = 0;
        }
        Assert.That(read, Is.Zero, "Медленное соединение не закрыто.");
      }

      Assert.That(await ConnectionCount(server), Is.EqualTo(1));
    }
    finally
    {
      foreach (var client in slow)
        client.Dispose();
    }
  }

  /// <summary>
  /// Шквал соединений, которые обрываются как попало (без закрытия, посреди сообщения, после мусора). Сервер не падает,
  /// не держит мёртвые соединения, а игроки за столом продолжают играть.
  /// </summary>
  [Test]
  public async Task ConnectionStorm_LeavesServerHealthy()
  {
    await using var server = WebSocketTableServer.Start(new IPEndPoint(IPAddress.Loopback, 0),
      Options with { MaxConnectionsPerAddress = 200, Table = Options.Table with { MaxConnections = 200 } });
    var uri = new Uri($"ws://127.0.0.1:{server.Port}/");
    using var host = new RemotePlayer(uri, "Аня");
    WaitFor(() => host.Client.Lobby is not null, TimeSpan.FromSeconds(5), host);

    var random = new Random(601);
    var storm = Enumerable.Range(0, 150).Select(async i =>
    {
      await Task.Delay(random.Next(0, 300));
      using var socket = new ClientWebSocket();
      try
      {
        await socket.ConnectAsync(uri, CancellationToken.None);
        switch (i % 4)
        {
          case 0:
            await socket.SendAsync(Encoding.UTF8.GetBytes("{\"type\":\"hello\",\"protocol\":1,\"name\":\"Шквал\"}"), WebSocketMessageType.Text, true, CancellationToken.None);
            break;
          case 1:
            await socket.SendAsync(Encoding.UTF8.GetBytes("{\"type\":\"hel"), WebSocketMessageType.Text, false, CancellationToken.None);
            break;
          case 2:
            await socket.SendAsync(new byte[] { 0xFF, 0xFE, 0x00 }, WebSocketMessageType.Binary, true, CancellationToken.None);
            break;
        }
      }
      catch (WebSocketException)
      {
      }
      // Обрыв без закрытия.
      socket.Abort();
    }).ToList();
    await Task.WhenAll(storm);

    // Мёртвые соединения уходят; за столом остаётся создатель (места шквала освободились при обрыве).
    var watch = Stopwatch.StartNew();
    while (await ConnectionCount(server) != 1)
    {
      Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(30)), "Сервер держит мёртвые соединения.");
      host.Link.Poll();
      await Task.Delay(50);
    }

    WaitFor(() => host.Client.Lobby!.Seats.Count == 1, TimeSpan.FromSeconds(10), host);
    host.Client.AddBot(BotLevel.Easy);
    host.Client.Start(GameMode.Money, true);
    WaitFor(() => host.Client.Game.Mirror is not null, TimeSpan.FromSeconds(10), host);
    Assert.That(host.Disconnects, Is.Empty);
  }
}
