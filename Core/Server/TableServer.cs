using System.Security.Cryptography;
using System.Text;
using ConspiracyLeague.Core.Bots;
using ConspiracyLeague.Core.Commands;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Logging;
using ConspiracyLeague.Core.Protocol;

namespace ConspiracyLeague.Core.Server;

/// <summary>
/// Соединение с клиентом — то, что серверу стола (<see cref="TableServer"/>) даёт транспорт.
/// </summary>
public interface IConnection
{
  /// <summary>
  /// Отправить сообщение. Не блокирует: транспорт ставит его в очередь.
  /// </summary>
  void Send(string json);

  /// <summary>
  /// Закрыть соединение. Транспорт потом сообщит об этом серверу (<see cref="TableServer.Disconnect"/>).
  /// </summary>
  void Close();
}

/// <summary>
/// Настройки сервера стола.
/// </summary>
public sealed record TableServerOptions
{
  /// <summary>
  /// Больше игроков за стол не сесть.
  /// </summary>
  public int MaxSeats { get; init; } = FieldLayout.MaxPlayers;

  /// <summary>
  /// Меньше игроков партию не начать.
  /// </summary>
  public int MinSeats { get; init; } = 2;

  /// <summary>
  /// Больше одновременных соединений сервер не принимает.
  /// </summary>
  public int MaxConnections { get; init; } = 32;

  /// <summary>
  /// За столько клиент должен представиться (<see cref="Hello"/>), иначе соединение закрывается.
  /// </summary>
  public TimeSpan HandshakeTimeout { get; init; } = TimeSpan.FromSeconds(10);

  /// <summary>
  /// Как часто сервер проверяет связь (<see cref="Ping"/>).
  /// </summary>
  public TimeSpan PingInterval { get; init; } = TimeSpan.FromSeconds(5);

  /// <summary>
  /// Клиента, от которого столько ничего не приходило, сервер отключает.
  /// </summary>
  public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromSeconds(20);

  /// <summary>
  /// Столько ждём действия игрока на связи, потом сервер действует за него (<see cref="TableServer"/>).
  /// <see cref="TimeSpan.Zero"/> — без ограничения. Создатель стола может задать своё время при начале партии
  /// (<see cref="LobbyStart.TurnSeconds"/>); это — если он не задал.
  /// </summary>
  public TimeSpan TurnTimeout { get; init; } = TimeSpan.FromSeconds(90);

  /// <summary>
  /// Столько ждём действия игрока, который потерял связь.
  /// </summary>
  public TimeSpan DisconnectedTurnTimeout { get; init; } = TimeSpan.FromSeconds(20);

  /// <summary>
  /// Пауза между ходами ботов, чтобы люди успевали следить.
  /// </summary>
  public TimeSpan BotDelay { get; init; } = TimeSpan.FromSeconds(0.9);

  /// <summary>
  /// Сколько сообщений клиент может прислать подряд.
  /// </summary>
  public int RateBurst { get; init; } = 40;

  /// <summary>
  /// Сколько сообщений в секунду клиент может присылать постоянно.
  /// </summary>
  public double RatePerSecond { get; init; } = 15;

  /// <summary>
  /// Столько нарушений (лишние или неверные сообщения) — и клиента отключают.
  /// </summary>
  public int MaxViolations { get; init; } = 50;

  /// <summary>
  /// Открыть журнал партии (<see cref="GameActionLog"/>), когда она начнётся; <c>null</c> — без журнала.
  /// Туда же пишутся события стола: кто подключился, потерял связь, не успел походить.
  /// </summary>
  public Func<GameActionLog>? ActionLog { get; init; }
}

/// <summary>
/// Сервер одного стола: игроки подключаются, знакомятся, садятся за стол (лобби), создатель стола добавляет ботов
/// и начинает партию, дальше партию ведёт <see cref="GameHost"/>. Ничего не знает о сети — с ним работает транспорт
/// через <see cref="IConnection"/>.
/// </summary>
/// <remarks>
/// <para>
/// Не потокобезопасен: транспорт вызывает его из одного потока (или по очереди). Время берёт из переданных часов —
/// так его можно проверять в тестах без ожидания.
/// </para>
/// <para>
/// Безопасность. Место игрока определяет соединение, а не сообщение. При знакомстве игрок получает токен места —
/// 256 случайных бит; сервер хранит только его хеш и сравнивает за постоянное время. С токеном игрок возвращается
/// на своё место после обрыва связи, без токена занять чужое место нельзя. Частота сообщений ограничена,
/// за неверные и лишние сообщения клиент получает нарушения и после <see cref="TableServerOptions.MaxViolations"/>
/// отключается. Молчащий клиент отключается по таймауту.
/// </para>
/// <para>
/// Надёжность. Обрыв связи не останавливает партию: место остаётся за игроком, а если он не успевает походить
/// (<see cref="TableServerOptions.TurnTimeout"/>, без связи — <see cref="TableServerOptions.DisconnectedTurnTimeout"/>),
/// сервер действует за него безопасно: отказывается от выбора или пасует.
/// </para>
/// </remarks>
public sealed class TableServer : IDisposable
{
  private const int MaxNameLength = 24;
  private const int TokenBytes = 32;

  private readonly TableServerOptions _options;
  private readonly Func<TimeSpan> _clock;
  private readonly Func<byte[]> _randomKey;
  private readonly Dictionary<IConnection, Peer> _peers = new(ReferenceEqualityComparer.Instance);
  private readonly List<SeatInfo> _seats = [];
  private GameHost? _host;
  private GameActionLog? _actionLog;
  private TimeSpan _lastBotStep;
  private (int Seat, long Version)? _turn;
  private TimeSpan _turnDeadline;

  /// <summary>
  /// Сколько ждать действия игрока на связи в этой партии; <c>null</c> — без ограничения.
  /// </summary>
  private TimeSpan? _turnLimit;
  private long _pingNonce;

  /// <param name="options">Настройки; по умолчанию — обычные.</param>
  /// <param name="clock">Текущее время (любой монотонный отсчёт); по умолчанию — часы процесса.</param>
  /// <param name="randomKey">Ключ случайности партии; по умолчанию — новый криптостойкий.</param>
  public TableServer(TableServerOptions? options = null, Func<TimeSpan>? clock = null, Func<byte[]>? randomKey = null)
  {
    _options = options ?? new TableServerOptions();
    if (clock is null)
    {
      var watch = System.Diagnostics.Stopwatch.StartNew();
      clock = () => watch.Elapsed;
    }

    _clock = clock;
    _randomKey = randomKey ?? SecureSeededRandom.NewKey;
  }

  /// <summary>
  /// Партия, если уже началась.
  /// </summary>
  public ServerGame? Game => _host?.Game;

  /// <summary>
  /// Сколько соединений открыто.
  /// </summary>
  public int ConnectionCount => _peers.Count;

  /// <summary>
  /// Что произошло на сервере — для журнала.
  /// </summary>
  public event Action<string>? Log;

  private TimeSpan Now => _clock();

  /// <summary>
  /// Закрыть журнал партии.
  /// </summary>
  public void Dispose()
  {
    _actionLog?.Dispose();
    _actionLog = null;
  }

  /// <summary>
  /// Новое соединение.
  /// </summary>
  /// <returns><c>false</c>, если соединений слишком много — транспорт закроет его.</returns>
  public bool Connect(IConnection connection)
  {
    ArgumentNullException.ThrowIfNull(connection);
    if (_peers.Count >= _options.MaxConnections)
      return false;

    _peers[connection] = new Peer(connection, Now, _options);
    return true;
  }

  /// <summary>
  /// Соединение закрыто (клиентом, сетью или сервером).
  /// </summary>
  public void Disconnect(IConnection connection)
  {
    if (!_peers.Remove(connection, out var peer) || peer.Seat is not { } seat || seat.Peer != peer)
      return;

    seat.Peer = null;
    if (_host is null)
    {
      // До начала партии место освобождается.
      _seats.Remove(seat);
      Log?.Invoke($"«{seat.Name}» ушёл из-за стола.");
    }
    else
    {
      Log?.Invoke($"«{seat.Name}» потерял связь.");
      // Если сейчас его ход, ждать долго незачем.
      if (_turn is { } turn && _seats[turn.Seat] == seat)
      {
        _turnDeadline = Min(_turnDeadline, Now + _options.DisconnectedTurnTimeout);
        BroadcastTimer(turn.Seat);
      }
    }

    BroadcastLobby();
  }

  /// <summary>
  /// Сообщение от клиента.
  /// </summary>
  public void Receive(IConnection connection, string json)
  {
    if (!_peers.TryGetValue(connection, out var peer))
      return;

    peer.LastHeard = Now;
    if (!peer.Rate.TryTake(Now))
    {
      Violate(peer, "Слишком много сообщений.");
      return;
    }

    ClientMessage message;
    try
    {
      message = ProtocolSerializer.DeserializeClient(json);
    }
    catch (FormatException)
    {
      peer.Send(new CommandReply(0, false, "Сервер не смог разобрать сообщение."));
      Violate(peer, "Неверные сообщения.", weight: 5);
      return;
    }

    if (peer.Seat is null)
    {
      if (message is Hello hello)
        Greet(peer, hello);
      else
        Drop(peer, "Сначала нужно представиться.", final: true);
      return;
    }

    var seat = peer.Seat;
    switch (message)
    {
      case Hello:
        Violate(peer, "Повторное знакомство.");
        break;
      case Pong:
        break;
      case Leave:
        Drop(peer, "Вы ушли из-за стола.", final: true);
        break;
      case LobbyAddBot addBot:
        AddBot(peer, seat, addBot.Level);
        break;
      case LobbyRemoveSeat remove:
        RemoveSeat(peer, seat, remove.Index);
        break;
      case LobbyStart start:
        Start(peer, seat, start);
        break;
      case CommandRequest or SyncRequest when _host is { } host:
        host.Receive(_seats.IndexOf(seat), message);
        break;
      case CommandRequest or SyncRequest:
        peer.Send(new Notice("Партия ещё не началась."));
        break;
    }
  }

  /// <summary>
  /// Время идёт: проверка связи, таймауты, ходы ботов, таймер хода. Транспорт вызывает это часто (например, 10 раз в секунду).
  /// </summary>
  public void Tick()
  {
    var now = Now;
    foreach (var peer in _peers.Values.ToList())
    {
      if (peer.Seat is null && now - peer.ConnectedAt > _options.HandshakeTimeout)
        Drop(peer, "Клиент не представился.", final: true);
      else if (now - peer.LastHeard > _options.IdleTimeout)
        Drop(peer, "Нет связи с клиентом.", final: false);
      else if (peer.Seat is not null && now - peer.LastPing >= _options.PingInterval)
      {
        peer.LastPing = now;
        peer.Send(new Ping(++_pingNonce));
      }
    }

    if (_host is not { } host || host.Game.Session.Phase == GamePhase.GameOver)
      return;

    if (now - _lastBotStep >= _options.BotDelay)
    {
      _lastBotStep = now;
      host.RunBotStep();
      if (host.Game.BotsStuck)
        Log?.Invoke("Боты не могут походить.");
    }

    UpdateTurnTimer(host, now);
  }

  private void Greet(Peer peer, Hello hello)
  {
    if (hello.Protocol != ProtocolInfo.Version)
    {
      Drop(peer, $"Версия игры не совпадает с сервером (протокол {hello.Protocol}, у сервера {ProtocolInfo.Version}). Обновите игру.", final: true);
      return;
    }

    // Возвращение на своё место по токену.
    if (hello.Token is { } token)
    {
      var hash = HashToken(token);
      var seat = hash is null ? null : _seats.FirstOrDefault(seat => seat.TokenHash is { } known && CryptographicOperations.FixedTimeEquals(known, hash));
      if (seat is not null)
      {
        if (seat.Peer is { } old)
        {
          // Сначала отвязываем старое соединение: иначе его закрытие освободило бы место в лобби.
          old.Seat = null;
          seat.Peer = null;
          Drop(old, "Вы подключились к столу с другого устройства.", final: true);
        }
        Seat(peer, seat, token);
        Log?.Invoke($"«{seat.Name}» вернулся за стол.");
        return;
      }

      if (_host is not null)
      {
        Drop(peer, "Вашего места за этим столом нет: партия уже идёт.", final: true);
        return;
      }
    }

    if (_host is not null)
    {
      Drop(peer, "Партия уже идёт — сесть за стол нельзя.", final: true);
      return;
    }
    if (_seats.Count >= _options.MaxSeats)
    {
      Drop(peer, "За столом нет свободных мест.", final: true);
      return;
    }

    var newToken = Base64Url(RandomNumberGenerator.GetBytes(TokenBytes));
    var created = new SeatInfo(UniqueName(hello.Name), isBot: false, BotLevel.Easy) { TokenHash = HashToken(newToken) };
    _seats.Add(created);
    Seat(peer, created, newToken);
    Log?.Invoke($"«{created.Name}» сел за стол.");
  }

  private void Seat(Peer peer, SeatInfo seat, string token)
  {
    peer.Seat = seat;
    seat.Peer = peer;
    peer.Send(new Welcome(ProtocolInfo.Version, token));
    BroadcastLobby();
    if (_host is { } host)
    {
      var index = _seats.IndexOf(seat);
      peer.Send(new GameStarted(index));
      host.Connect(index);
      if (_turn is { } turn && turn.Seat == index)
      {
        // Время без ограничения снова без ограничения: связь вернулась. Ограниченное не продлевается —
        // иначе переподключением можно было бы тянуть ход бесконечно.
        if (_turnLimit is null)
          _turnDeadline = TimeSpan.MaxValue;
        BroadcastTimer(index);
      }
    }
  }

  private void AddBot(Peer peer, SeatInfo seat, BotLevel level)
  {
    if (!RequireHostInLobby(peer, seat))
      return;
    if (!Enum.IsDefined(level))
    {
      Violate(peer, "Неверный уровень бота.");
      return;
    }
    if (_seats.Count >= _options.MaxSeats)
    {
      peer.Send(new Notice("За столом нет свободных мест."));
      return;
    }

    var bots = _seats.Count(other => other.IsBot);
    _seats.Add(new SeatInfo(UniqueName($"Бот {bots + 1}"), isBot: true, level));
    BroadcastLobby();
  }

  private void RemoveSeat(Peer peer, SeatInfo seat, int index)
  {
    if (!RequireHostInLobby(peer, seat))
      return;
    if (index < 0 || index >= _seats.Count || _seats[index] == seat)
    {
      peer.Send(new Notice("Это место убрать нельзя."));
      return;
    }

    var removed = _seats[index];
    _seats.RemoveAt(index);
    if (removed.Peer is { } other)
    {
      removed.Peer = null;
      other.Seat = null;
      Drop(other, "Создатель стола убрал ваше место.", final: true);
    }

    BroadcastLobby();
  }

  private void Start(Peer peer, SeatInfo seat, LobbyStart start)
  {
    if (!RequireHostInLobby(peer, seat))
      return;
    if (!Enum.IsDefined(start.Mode))
    {
      Violate(peer, "Неверный режим.");
      return;
    }
    if (_seats.Count < _options.MinSeats)
    {
      peer.Send(new Notice($"Нужно хотя бы {_options.MinSeats} игрока: добавьте ботов или дождитесь людей."));
      return;
    }
    if (start.TurnSeconds is < 0 or > LobbyStart.MaxTurnSeconds)
    {
      peer.Send(new Notice($"Время хода — от 0 (без ограничения) до {LobbyStart.MaxTurnSeconds} секунд."));
      return;
    }

    var limit = start.TurnSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : _options.TurnTimeout;
    _turnLimit = limit > TimeSpan.Zero ? limit : null;

    var colors = Enum.GetValues<PlayerColor>();
    var setup = new GameSetup(
      _seats.Select((info, index) => new SeatSetup(info.Name, colors[index], info.IsBot, info.BotLevel)).ToList(),
      start.Mode,
      start.FailureConsequences,
      _randomKey());
    var host = new GameHost(new ServerGame(setup));
    host.Outgoing += (index, json) => _seats[index].Peer?.Connection.Send(json);
    host.Game.Faulted += (index, command, error) => Log?.Invoke($"Сбой команды {command} игрока {index}: {error}");
    StartActionLog(host.Game);
    _host = host;
    _lastBotStep = Now;
    Log?.Invoke($"Партия началась: {_seats.Count} игроков, режим {start.Mode}, время хода "
      + (_turnLimit is { } turnLimit ? $"{turnLimit.TotalSeconds:0} с." : "без ограничения."));

    BroadcastLobby();
    for (var index = 0; index < _seats.Count; index++)
    {
      if (_seats[index].Peer is { } player)
      {
        player.Send(new GameStarted(index));
        host.Connect(index);
      }
    }
  }

  private void StartActionLog(ServerGame game)
  {
    if (_options.ActionLog is not { } open)
      return;

    try
    {
      _actionLog = open();
    }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
    {
      // Без журнала партия всё равно идёт.
      Log?.Invoke($"Не удалось открыть журнал партии: {error.Message}");
      return;
    }

    var log = _actionLog;
    log.Attach(game.Session);
    Log += log.Write;
    game.SessionReplaced += session =>
    {
      log.Write("Сбой: последняя команда отменена, партия восстановлена из журнала команд.");
      log.Attach(session);
    };
  }

  private bool RequireHostInLobby(Peer peer, SeatInfo seat)
  {
    if (_host is not null)
    {
      peer.Send(new Notice("Партия уже идёт."));
      return false;
    }
    if (Host != seat)
    {
      peer.Send(new Notice("Это может только создатель стола."));
      return false;
    }

    return true;
  }

  /// <summary>
  /// Создатель стола — первый игрок-человек за столом.
  /// </summary>
  private SeatInfo? Host => _seats.FirstOrDefault(seat => !seat.IsBot);

  private void UpdateTurnTimer(GameHost host, TimeSpan now)
  {
    var game = host.Game;
    if (game.ActingSeat is not { } acting || game.IsBotSeat(acting) || game.IsBroken)
    {
      _turn = null;
      return;
    }

    var turn = (acting, game.Version);
    if (_turn != turn)
    {
      _turn = turn;
      // Игрока без связи долго не ждём, даже если время хода не ограничено: иначе партия встала бы навсегда.
      _turnDeadline = _seats[acting].Peer is null ? now + _options.DisconnectedTurnTimeout : Deadline(now);
      BroadcastTimer(acting);
      return;
    }

    if (now < _turnDeadline)
      return;

    // Игрок не успел: сервер действует за него безопасно — отказывается от выбора или пасует.
    Log?.Invoke($"«{_seats[acting].Name}» не успел походить — сервер действует за него.");
    var result = host.ExecuteForPlayer(acting, AutoCommand(game.Session, acting));
    if (!result.Accepted && game.Session.CheckNoPending().IsAllowed)
      result = host.ExecuteForPlayer(acting, new PassCommand());
    if (!result.Accepted)
    {
      Log?.Invoke($"Сервер не смог походить за «{_seats[acting].Name}»: {result.Error}");
      // Не повторяем каждый тик: ждём ещё один полный срок.
      _turnDeadline = _turnLimit is { } turnLimit ? now + turnLimit : now + _options.DisconnectedTurnTimeout;
    }
  }

  private static GameCommand AutoCommand(GameSession session, int seat)
  {
    if (session.PendingTheoryTrigger is not null)
      return new SkipTheoryTriggerCommand();
    if (session.PendingPlacement is { } choice)
    {
      if (choice.Effect.TargetIsOptional)
        return new ResolvePlacementCommand();
      var target = choice.Effect.TargetCells(session.PlacementContext(choice)).FirstOrDefault(target => target.Check.IsAllowed);
      return new ResolvePlacementCommand(target.Check?.IsAllowed == true ? target.Cell : null);
    }

    return new PassCommand();
  }

  /// <summary>
  /// Когда истечёт время хода игрока на связи, если ход начался сейчас.
  /// </summary>
  private TimeSpan Deadline(TimeSpan now) => _turnLimit is { } limit ? now + limit : TimeSpan.MaxValue;

  /// <summary>
  /// Сколько секунд осталось; <c>null</c> — время не ограничено.
  /// </summary>
  private int? SecondsLeft() =>
    _turnDeadline == TimeSpan.MaxValue ? null : Math.Max(0, (int)Math.Ceiling((_turnDeadline - Now).TotalSeconds));

  private void BroadcastTimer(int seat)
  {
    var message = new TurnTimer(seat, SecondsLeft());
    foreach (var info in _seats)
      info.Peer?.Send(message);
  }

  private void BroadcastLobby()
  {
    var host = Host;
    var seats = _seats.Select(seat => new LobbySeat(seat.Name, seat.IsBot, seat.BotLevel, seat.IsBot || seat.Peer is not null, seat == host)).ToList();
    for (var index = 0; index < _seats.Count; index++)
      _seats[index].Peer?.Send(new LobbyState(seats, index, _host is not null));
  }

  private void Violate(Peer peer, string reason, int weight = 1)
  {
    peer.Violations += weight;
    if (peer.Violations >= _options.MaxViolations)
      Drop(peer, $"Соединение закрыто: {reason}", final: true);
  }

  /// <summary>
  /// Попрощаться и закрыть соединение. Место игрока остаётся за ним (во время партии) или освобождается (в лобби) —
  /// это решит <see cref="Disconnect"/>, когда транспорт сообщит о закрытии.
  /// </summary>
  private void Drop(Peer peer, string reason, bool final)
  {
    peer.Send(new Disconnecting(reason, final));
    peer.Connection.Close();
    Disconnect(peer.Connection);
  }

  private string UniqueName(string name)
  {
    var clean = new string((name ?? string.Empty).Where(c => !char.IsControl(c) && !char.IsSurrogate(c)).ToArray()).Trim();
    if (clean.Length > MaxNameLength)
      clean = clean[..MaxNameLength].Trim();
    if (clean.Length == 0)
      clean = "Игрок";

    var unique = clean;
    for (var n = 2; _seats.Any(seat => string.Equals(seat.Name, unique, StringComparison.OrdinalIgnoreCase)); n++)
      unique = $"{clean} {n}";
    return unique;
  }

  private static byte[]? HashToken(string token) =>
    token.Length is > 0 and <= 128 ? SHA256.HashData(Encoding.UTF8.GetBytes(token)) : null;

  private static string Base64Url(byte[] bytes) =>
    Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

  private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

  /// <summary>
  /// Место за столом. Класс, а не запись: места сравниваются как объекты.
  /// </summary>
  private sealed class SeatInfo(string name, bool isBot, BotLevel botLevel)
  {
    public string Name { get; } = name;

    public bool IsBot { get; } = isBot;

    public BotLevel BotLevel { get; } = botLevel;

    public byte[]? TokenHash { get; init; }

    public Peer? Peer { get; set; }
  }

  private sealed class Peer(IConnection connection, TimeSpan now, TableServerOptions options)
  {
    public IConnection Connection { get; } = connection;

    public TimeSpan ConnectedAt { get; } = now;

    public TimeSpan LastHeard { get; set; } = now;

    public TimeSpan LastPing { get; set; } = now;

    public SeatInfo? Seat { get; set; }

    public int Violations { get; set; }

    public TokenBucket Rate { get; } = new(options.RateBurst, options.RatePerSecond, now);

    public void Send(ServerMessage message) => Connection.Send(ProtocolSerializer.Serialize(message));
  }

  /// <summary>
  /// Ограничение частоты: ведро на <paramref name="capacity"/> сообщений, пополняется на <paramref name="perSecond"/> в секунду.
  /// </summary>
  private sealed class TokenBucket(int capacity, double perSecond, TimeSpan now)
  {
    private double _tokens = capacity;
    private TimeSpan _last = now;

    public bool TryTake(TimeSpan now)
    {
      _tokens = Math.Min(capacity, _tokens + (now - _last).TotalSeconds * perSecond);
      _last = now;
      if (_tokens < 1)
        return false;
      _tokens--;
      return true;
    }
  }
}
