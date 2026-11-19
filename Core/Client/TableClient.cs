using ConspiracyLeague.Core.Bots;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Protocol;

namespace ConspiracyLeague.Core.Client;

/// <summary>
/// Клиент стола: знакомится с сервером, сидит в лобби, а когда партия начнётся — отдаёт её сообщения клиенту партии
/// (<see cref="Game"/>). Ничего не знает о сети: транспорт сообщает ему о подключении (<see cref="OnConnected"/>)
/// и передаёт принятые сообщения (<see cref="Receive"/>).
/// </summary>
/// <remarks>
/// После обрыва связи транспорт подключается снова, и клиент представляется с токеном места — сервер вернёт его на то же
/// место, пришлёт партию целиком, а неотвеченную команду клиент отправит ещё раз (сервер не выполнит её дважды).
/// </remarks>
public sealed class TableClient
{
  private readonly Action<string> _send;
  private readonly string _name;

  /// <param name="send">Отправить сообщение на сервер.</param>
  /// <param name="name">Имя игрока.</param>
  /// <param name="token">Токен места, если клиент возвращается за стол.</param>
  public TableClient(Action<string> send, string name, string? token = null)
  {
    ArgumentNullException.ThrowIfNull(send);
    _send = send;
    _name = name;
    Token = token;
    Game = new GameClient(send, othersAsBots: true);
  }

  /// <summary>
  /// Токен места от сервера (<see cref="Welcome"/>).
  /// </summary>
  public string? Token { get; private set; }

  /// <summary>
  /// Кто сидит за столом.
  /// </summary>
  public LobbyState? Lobby { get; private set; }

  /// <summary>
  /// Место в партии, когда она началась.
  /// </summary>
  public int? Seat { get; private set; }

  /// <summary>
  /// Последний таймер хода от сервера: чьего действия ждут и сколько секунд оставалось.
  /// </summary>
  public (int Seat, int? Seconds)? LastTimer { get; private set; }

  /// <summary>
  /// Этот клиент — создатель стола.
  /// </summary>
  public bool IsHost => Lobby is { } lobby && lobby.You >= 0 && lobby.You < lobby.Seats.Count && lobby.Seats[lobby.You].IsHost;

  /// <summary>
  /// Клиент партии: отправляет команды и собирает партию по видам.
  /// </summary>
  public GameClient Game { get; }

  /// <summary>
  /// Сервер прощается: возвращаться бесполезно (<see cref="Disconnecting.Final"/>) — транспорту не нужно переподключаться.
  /// </summary>
  public bool Closed { get; private set; }

  /// <summary>
  /// Изменилось лобби.
  /// </summary>
  public event Action<LobbyState>? LobbyChanged;

  /// <summary>
  /// Партия началась или клиент вернулся в неё; аргумент — место.
  /// </summary>
  public event Action<int>? GameStarted;

  /// <summary>
  /// Сообщение от сервера для игрока.
  /// </summary>
  public event Action<string>? Notice;

  /// <summary>
  /// Сервер закрывает соединение; аргументы — причина и можно ли вернуться.
  /// </summary>
  public event Action<string, bool>? Disconnecting;

  /// <summary>
  /// Ждём действия игрока; аргументы — место и секунды.
  /// </summary>
  public event Action<int, int?>? TurnTimer;

  /// <summary>
  /// Транспорт подключился (или переподключился): представиться.
  /// </summary>
  public void OnConnected()
  {
    if (Closed)
      return;
    _send(ProtocolSerializer.Serialize(new Hello(ProtocolInfo.Version, _name, Token)));
  }

  /// <summary>
  /// Сообщение от сервера.
  /// </summary>
  public void Receive(string json)
  {
    ServerMessage message;
    try
    {
      message = ProtocolSerializer.DeserializeServer(json);
    }
    catch (FormatException error)
    {
      Notice?.Invoke($"Ошибка связи с сервером: {error.Message}");
      return;
    }

    switch (message)
    {
      case Welcome welcome:
        Token = welcome.Token;
        break;
      case Ping ping:
        _send(ProtocolSerializer.Serialize(new Pong(ping.Nonce)));
        break;
      case LobbyState lobby:
        Lobby = lobby;
        LobbyChanged?.Invoke(lobby);
        break;
      case Protocol.GameStarted started:
        var returning = Seat == started.Seat;
        Seat = started.Seat;
        GameStarted?.Invoke(started.Seat);
        // Вернулись после обрыва: ответ на последнюю команду мог потеряться.
        if (returning)
          Game.ResendPending();
        break;
      case Protocol.TurnTimer timer:
        LastTimer = (timer.Seat, timer.Seconds);
        TurnTimer?.Invoke(timer.Seat, timer.Seconds);
        break;
      case Protocol.Notice notice:
        Notice?.Invoke(notice.Text);
        break;
      case Protocol.Disconnecting bye:
        if (bye.Final)
          Closed = true;
        Disconnecting?.Invoke(bye.Reason, bye.Final);
        break;
      default:
        Game.Receive(message);
        break;
    }
  }

  /// <summary>
  /// Добавить бота (может только создатель стола).
  /// </summary>
  public void AddBot(BotLevel level) => _send(ProtocolSerializer.Serialize(new LobbyAddBot(level)));

  /// <summary>
  /// Убрать место (может только создатель стола).
  /// </summary>
  public void RemoveSeat(int index) => _send(ProtocolSerializer.Serialize(new LobbyRemoveSeat(index)));

  /// <summary>
  /// Начать партию (может только создатель стола).
  /// </summary>
  /// <param name="turnSeconds">Время хода в секундах; 0 — без ограничения; <c>null</c> — как настроено на сервере.</param>
  public void Start(GameMode mode, bool failureConsequences, int? turnSeconds = null) =>
    _send(ProtocolSerializer.Serialize(new LobbyStart(mode, failureConsequences, turnSeconds)));

  /// <summary>
  /// Уйти из-за стола.
  /// </summary>
  public void Leave()
  {
    Closed = true;
    _send(ProtocolSerializer.Serialize(new Leave()));
  }
}
