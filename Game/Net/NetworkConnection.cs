using System.Net;
using System.Net.Sockets;
using ConspiracyLeague.Core.Client;
using ConspiracyLeague.Core.Logging;
using ConspiracyLeague.Core.Net;
using ConspiracyLeague.Core.Server;
using Godot;

namespace ConspiracyLeague.Game.Net;

/// <summary>
/// Подключение к столу: связь с сервером (<see cref="WebSocketLink"/>) и клиент стола (<see cref="TableClient"/>),
/// а если стол создан на этом компьютере — и сам сервер. Переходит из лобби в партию целиком: связь не рвётся.
/// </summary>
public sealed class NetworkConnection : IDisposable
{
  /// <summary>
  /// Порт по умолчанию.
  /// </summary>
  public const int DefaultPort = 7777;

  private bool _disposed;

  private NetworkConnection(Uri address, string name, string? fingerprint, WebSocketTableServer? server)
  {
    Address = address;
    Server = server;
    Link = new WebSocketLink(address, fingerprint);
    Table = new TableClient(Link.Send, name);
    Link.Connected += Table.OnConnected;
    Link.Received += json =>
    {
      Table.Receive(json);
      // Сервер попрощался насовсем: переподключаться бесполезно.
      if (Table.Closed)
        Link.Stop();
    };
    Link.Start();
  }

  /// <summary>
  /// Адрес сервера.
  /// </summary>
  public Uri Address { get; }

  /// <summary>
  /// Связь с сервером.
  /// </summary>
  public WebSocketLink Link { get; }

  /// <summary>
  /// Клиент стола.
  /// </summary>
  public TableClient Table { get; }

  /// <summary>
  /// Сервер, если стол создан на этом компьютере.
  /// </summary>
  public WebSocketTableServer? Server { get; }

  /// <summary>
  /// Подключиться к чужому столу.
  /// </summary>
  /// <param name="address">Адрес: «хост:порт», «хост» или полный ws:// / wss://.</param>
  /// <param name="name">Имя игрока.</param>
  /// <param name="secure">Адрес без схемы — по TLS (wss://, через интернет) или без шифрования (ws://, локальная сеть).</param>
  /// <param name="fingerprint">Отпечаток сертификата сервера (для своего сервера с самоподписанным сертификатом).</param>
  /// <exception cref="ArgumentException">Неверный адрес или отпечаток.</exception>
  public static NetworkConnection Join(string address, string name, bool secure, string? fingerprint = null) =>
    new(ParseAddress(address, secure), name, fingerprint, null);

  /// <summary>
  /// Создать стол на этом компьютере и сесть за него. Соединения не шифруются — стол для локальной сети;
  /// для игры через интернет нужен отдельный сервер с сертификатом.
  /// </summary>
  /// <exception cref="SocketException">Порт занят.</exception>
  public static NetworkConnection Host(int port, string name)
  {
    // Журнал стола — на компьютере создателя, а он тоже игрок: скрытое (карты взакрытую) туда не пишется.
    var logs = ProjectSettings.GlobalizePath(GameLogs.Directory);
    var server = WebSocketTableServer.Start(new IPEndPoint(IPAddress.Any, port), new WebSocketServerOptions
    {
      Table = new TableServerOptions
      {
        ActionLog = () =>
        {
          var (log, path) = GameActionLog.OpenFile(logs, "table", revealHidden: false);
          GD.Print($"Журнал партии: {path}");
          return log;
        },
      },
    });
    server.Log += text => GD.Print($"[сервер] {text}");
    return new NetworkConnection(new Uri($"ws://127.0.0.1:{server.Port}/"), name, null, server);
  }

  /// <summary>
  /// Адреса этого компьютера в локальной сети — их создатель стола сообщает друзьям.
  /// </summary>
  public static IReadOnlyList<string> LocalAddresses()
  {
    try
    {
      return Dns.GetHostAddresses(Dns.GetHostName())
        .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
        .Select(address => address.ToString())
        .ToList();
    }
    catch (SocketException)
    {
      return [];
    }
  }

  /// <summary>
  /// Забрать события связи (в главном потоке).
  /// </summary>
  public void Poll() => Link.Poll();

  /// <summary>
  /// Уйти из-за стола и закрыть связь (и сервер, если он здесь).
  /// </summary>
  public void Dispose()
  {
    if (_disposed)
      return;
    _disposed = true;

    if (Link.IsConnected && !Table.Closed)
      Table.Leave();
    var link = Link;
    var server = Server;
    // Даём прощанию уйти, потом закрываем.
    _ = Task.Run(async () =>
    {
      await Task.Delay(300);
      link.Dispose();
      if (server is not null)
        await server.DisposeAsync();
    });
  }

  private static Uri ParseAddress(string address, bool secure)
  {
    var text = address.Trim();
    if (text.Length == 0 || text is "ws://" or "wss://")
      throw new ArgumentException("Введите адрес сервера.");
    if (!text.Contains("://"))
      text = secure ? $"wss://{text}" : $"ws://{text}";
    if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("ws" or "wss") || uri.Host.Length == 0)
      throw new ArgumentException("Адрес сервера: «хост:порт» или ws://… / wss://….");
    if (uri.IsDefaultPort && !address.Contains("://"))
      uri = new UriBuilder(uri) { Port = DefaultPort }.Uri;
    return uri;
  }
}
