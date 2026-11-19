using ConspiracyLeague.Core.Bots;
using ConspiracyLeague.Core.Client;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Protocol;
using ConspiracyLeague.Core.Server;
using ConspiracyLeague.Core.Views;

namespace ConspiracyLeague.Core.Tests;

public sealed class TableServerTests
{
  private static readonly TableServerOptions Fast = new()
  {
    BotDelay = TimeSpan.FromMilliseconds(10),
    TurnTimeout = TimeSpan.FromSeconds(90),
    RatePerSecond = 1000,
  };

  /// <summary>
  /// Стол без сети: сообщения идут через очередь (как по сети — не вложенными вызовами), время — ручное.
  /// </summary>
  private sealed class Net
  {
    private readonly Queue<Action> _queue = new();

    public Net(TableServerOptions? options = null)
    {
      Server = new TableServer(options ?? Fast, () => Now, () => NetworkBotGame.KeyFrom(7));
      Server.Log += Log.Add;
    }

    public TableServer Server { get; }

    public TimeSpan Now { get; set; }

    public List<string> Log { get; } = [];

    public Player Join(string name, string? token = null)
    {
      var player = Connect(name, token);
      player.Hello();
      Pump();
      return player;
    }

    public Player Connect(string name, string? token = null)
    {
      var player = new Player(this, name, token);
      Assert.That(Server.Connect(player.Connection), Is.True);
      return player;
    }

    public void Post(Action action) => _queue.Enqueue(action);

    public void Pump()
    {
      while (_queue.TryDequeue(out var action))
        action();
    }

    public void Advance(TimeSpan time, int ticks = 1)
    {
      for (var i = 0; i < ticks; i++)
      {
        Now += time / ticks;
        Server.Tick();
        Pump();
      }
    }
  }

  private sealed class Connection(Net net) : IConnection
  {
    public Player Owner { get; set; } = null!;

    public bool Closed { get; private set; }

    /// <summary>
    /// Связь оборвалась: сообщения в пути потеряны (в отличие от закрытия сервером — тогда очередь сначала уходит).
    /// </summary>
    public bool Broken { get; private set; }

    public void Send(string json)
    {
      if (!Closed)
        net.Post(() =>
        {
          if (!Broken)
            Owner.Deliver(json);
        });
    }

    public void Close() => Closed = true;

    public void Break()
    {
      Broken = true;
      Closed = true;
    }
  }

  private sealed class Player
  {
    private readonly Net _net;

    public Player(Net net, string name, string? token)
    {
      _net = net;
      Connection = new Connection(net) { Owner = this };
      Client = new TableClient(Send, name, token);
      Client.Disconnecting += (reason, final) => Goodbyes.Add((reason, final));
      Client.Notice += Notices.Add;
      Bot = new MirrorBot(Client.Game);
    }

    public Connection Connection { get; private set; }

    public TableClient Client { get; }

    /// <summary>
    /// Транспорт переподключился: новое соединение, тот же клиент (он представится с токеном).
    /// </summary>
    public void Reconnect()
    {
      Connection = new Connection(_net) { Owner = this };
      Assert.That(_net.Server.Connect(Connection), Is.True);
      Client.OnConnected();
    }

    public MirrorBot Bot { get; }

    public List<(string Reason, bool Final)> Goodbyes { get; } = [];

    public List<string> Notices { get; } = [];

    public List<string> Received { get; } = [];

    public void Hello() => Client.OnConnected();

    public void Deliver(string json)
    {
      Received.Add(json);
      Client.Receive(json);
    }

    public void SendRaw(string json)
    {
      var connection = Connection;
      _net.Post(() =>
      {
        if (!connection.Broken)
          _net.Server.Receive(connection, json);
      });
    }

    private void Send(string json)
    {
      if (!Connection.Closed)
        SendRaw(json);
    }

    /// <summary>
    /// Связь оборвалась: транспорт сообщает серверу.
    /// </summary>
    public void Drop()
    {
      Connection.Break();
      _net.Server.Disconnect(Connection);
    }
  }

  [Test]
  public void Lobby_HostAddsBotsAndStarts()
  {
    var net = new Net();
    var host = net.Join("Аня");
    var guest = net.Join("Аня");
    host.Client.AddBot(BotLevel.Hard);
    guest.Client.AddBot(BotLevel.Easy);
    guest.Client.Start(GameMode.Money, true);
    net.Pump();

    Assert.Multiple(() =>
    {
      Assert.That(host.Client.IsHost, Is.True);
      Assert.That(guest.Client.IsHost, Is.False);
      Assert.That(guest.Client.Lobby!.Seats.Select(seat => seat.Name), Is.EqualTo(new[] { "Аня", "Аня 2", "Бот 1" }));
      Assert.That(guest.Notices, Has.Some.Contains("создатель стола"));
      Assert.That(net.Server.Game, Is.Null);
    });

    host.Client.Start(GameMode.Race, false);
    net.Pump();

    Assert.Multiple(() =>
    {
      Assert.That(net.Server.Game, Is.Not.Null);
      Assert.That(host.Client.Seat, Is.EqualTo(0));
      Assert.That(guest.Client.Seat, Is.EqualTo(1));
      Assert.That(guest.Client.Game.Mirror!.View.Viewer, Is.EqualTo(1));
      Assert.That(net.Server.Game!.IsBotSeat(2), Is.True);
      Assert.That(net.Server.Game.Session.Mode, Is.EqualTo(GameMode.Race));
      Assert.That(net.Server.Game.Session.Field.FailureConsequences, Is.False);
    });
  }

  [Test]
  public void Lobby_NamesAreCleaned()
  {
    var net = new Net();
    var player = net.Join("  \u0007Очень длинное имя игрока, которое не влезет  ");
    var empty = net.Join("\n\t");
    Assert.That(player.Client.Lobby!.Seats[0].Name, Is.EqualTo("Очень длинное имя игрока"));
    Assert.That(empty.Client.Lobby!.Seats[1].Name, Is.EqualTo("Игрок"));
  }

  [Test]
  public void Lobby_HostLeaves_NextHumanBecomesHost()
  {
    var net = new Net();
    var host = net.Join("Аня");
    var guest = net.Join("Боря");
    host.Client.Leave();
    net.Pump();

    Assert.That(guest.Client.Lobby!.Seats.Single().Name, Is.EqualTo("Боря"));
    Assert.That(guest.Client.IsHost, Is.True);
  }

  [Test]
  public void Lobby_HostRemovesPlayer()
  {
    var net = new Net();
    var host = net.Join("Аня");
    var guest = net.Join("Боря");
    host.Client.RemoveSeat(1);
    net.Pump();

    Assert.That(guest.Goodbyes, Has.Some.Matches<(string Reason, bool Final)>(bye => bye.Final));
    Assert.That(guest.Connection.Closed, Is.True);
    Assert.That(host.Client.Lobby!.Seats, Has.Count.EqualTo(1));
  }

  [Test]
  public void Lobby_IsFull()
  {
    var net = new Net(Fast with { MaxSeats = 2 });
    net.Join("Аня");
    net.Join("Боря");
    var third = net.Join("Вера");
    Assert.That(third.Goodbyes.Single().Reason, Does.Contain("нет свободных мест"));
  }

  [Test]
  public void WrongProtocolVersion_IsRejected()
  {
    var net = new Net();
    var player = net.Connect("Аня");
    player.SendRaw(ProtocolSerializer.Serialize(new Hello(ProtocolInfo.Version + 1, "Аня")));
    net.Pump();

    Assert.That(player.Goodbyes.Single().Final, Is.True);
    Assert.That(player.Goodbyes.Single().Reason, Does.Contain("Версия"));
    Assert.That(player.Connection.Closed, Is.True);
  }

  [Test]
  public void MessagesBeforeHello_CloseConnection()
  {
    var net = new Net();
    var player = net.Connect("Аня");
    player.SendRaw(ProtocolSerializer.Serialize(new LobbyStart(GameMode.Money, true)));
    net.Pump();
    Assert.That(player.Connection.Closed, Is.True);
  }

  [Test]
  public void SilentClient_IsDroppedAfterHandshakeTimeout()
  {
    var net = new Net();
    var player = net.Connect("Аня");
    net.Advance(TimeSpan.FromSeconds(9));
    Assert.That(player.Connection.Closed, Is.False);
    net.Advance(TimeSpan.FromSeconds(2));
    Assert.That(player.Connection.Closed, Is.True);
    Assert.That(net.Server.ConnectionCount, Is.Zero);
  }

  /// <summary>
  /// Клиент, который отвечает на проверку связи, живёт; переставший отвечать отключается, и в лобби его место освобождается.
  /// </summary>
  [Test]
  public void Heartbeat_KeepsAliveAndDropsSilent()
  {
    var net = new Net();
    var alive = net.Join("Аня");
    var silent = net.Join("Боря");
    silent.Connection.Close();
    net.Advance(TimeSpan.FromSeconds(60), ticks: 600);

    Assert.That(alive.Connection.Closed, Is.False);
    Assert.That(net.Server.ConnectionCount, Is.EqualTo(1));
    Assert.That(alive.Client.Lobby!.Seats.Select(seat => seat.Name), Is.EqualTo(new[] { "Аня" }));
  }

  [Test]
  public void Flood_IsLimitedAndThenDropped()
  {
    var net = new Net();
    var player = net.Join("Аня");
    for (var i = 0; i < 200; i++)
      player.SendRaw(ProtocolSerializer.Serialize(new Pong(i)));
    net.Pump();

    Assert.That(player.Connection.Closed, Is.True);
    Assert.That(player.Goodbyes.Single().Reason, Does.Contain("Слишком много сообщений"));
  }

  [Test]
  public void Garbage_IsAnsweredAndThenDropped()
  {
    var net = new Net();
    var player = net.Join("Аня");
    for (var i = 0; i < 9; i++)
    {
      player.SendRaw("{\"type\":\"hack\"}");
      net.Advance(TimeSpan.FromSeconds(1));
    }

    Assert.That(player.Connection.Closed, Is.False);
    player.SendRaw("{\"type\":\"hack\"}");
    net.Pump();
    Assert.That(player.Connection.Closed, Is.True);
  }

  private static (Net Net, Player Host, Player Guest) StartedGame(TableServerOptions? options = null, int? turnSeconds = null)
  {
    var net = new Net(options);
    var host = net.Join("Аня");
    var guest = net.Join("Боря");
    host.Client.AddBot(BotLevel.Easy);
    host.Client.Start(GameMode.Money, true, turnSeconds);
    net.Pump();
    return (net, host, guest);
  }

  /// <summary>
  /// Обрыв связи посреди партии: место остаётся за игроком, по токену он возвращается на него и получает партию целиком.
  /// Чужой или выдуманный токен место не даёт.
  /// </summary>
  [Test]
  public void Reconnect_WithTokenReturnsToSeat()
  {
    var (net, host, guest) = StartedGame();
    var token = guest.Client.Token!;
    guest.Drop();
    net.Pump();
    Assert.That(host.Client.Lobby!.Seats[1].Connected, Is.False);

    var stranger = net.Join("Вор", "выдуманный-токен");
    Assert.That(stranger.Goodbyes.Single().Final, Is.True);

    var back = net.Join("Боря", token);
    Assert.Multiple(() =>
    {
      Assert.That(back.Client.Seat, Is.EqualTo(1));
      Assert.That(host.Client.Lobby!.Seats[1].Connected, Is.True);
      Assert.That(ViewSerializer.Serialize(back.Client.Game.Mirror!.View), Is.EqualTo(ViewSerializer.Serialize(net.Server.Game!.ViewFor(1))));
    });
  }

  [Test]
  public void SameToken_FromAnotherDevice_ReplacesOldConnection()
  {
    var (net, _, guest) = StartedGame();
    var second = net.Join("Боря", guest.Client.Token);

    Assert.That(guest.Connection.Closed, Is.True);
    Assert.That(guest.Goodbyes.Single().Reason, Does.Contain("другого устройства"));
    Assert.That(second.Client.Seat, Is.EqualTo(1));
  }

  [Test]
  public void JoiningStartedGame_IsRejected()
  {
    var (net, _, _) = StartedGame();
    var late = net.Join("Вера");
    Assert.That(late.Goodbyes.Single().Reason, Does.Contain("Партия уже идёт"));
  }

  /// <summary>
  /// Игрок не успел походить — сервер пасует за него; без связи ждёт меньше.
  /// </summary>
  [Test]
  public void TurnTimer_PassesForSlowPlayer()
  {
    var (net, host, guest) = StartedGame(Fast with { TurnTimeout = TimeSpan.FromSeconds(30), DisconnectedTurnTimeout = TimeSpan.FromSeconds(5) });
    var game = net.Server.Game!;
    var timers = new List<(int Seat, int? Seconds)>();
    host.Client.TurnTimer += (seat, seconds) => timers.Add((seat, seconds));
    // Ждём очереди человека: бот ходит быстро.
    for (var i = 0; i < 1000 && game.ActingSeat is not (0 or 1); i++)
      net.Advance(TimeSpan.FromMilliseconds(20));
    var acting = game.ActingSeat!.Value;
    Assert.That(acting, Is.LessThan(2));
    net.Advance(TimeSpan.FromMilliseconds(20));

    net.Advance(TimeSpan.FromSeconds(29), ticks: 290);
    Assert.That(game.Session.Turns.HasPassed(game.Session.Players[acting]), Is.False);
    net.Advance(TimeSpan.FromSeconds(2), ticks: 20);
    Assert.That(game.Session.Turns.HasPassed(game.Session.Players[acting]), Is.True);
    Assert.That(timers, Has.Some.Matches<(int Seat, int? Seconds)>(timer => timer.Seat == acting && timer.Seconds is 29 or 30));

    // Второй человек без связи: сервер ждёт его недолго — партия идёт дальше без него.
    var other = host.Client.Seat == acting ? guest : host;
    var otherSeat = other.Client.Seat!.Value;
    other.Drop();
    var round = game.Session.Round;
    net.Advance(TimeSpan.FromSeconds(10), ticks: 500);
    Assert.That(game.Session.Turns.HasPassed(game.Session.Players[otherSeat]) || game.Session.Round > round, Is.True);
  }

  /// <summary>
  /// Партия целиком через сервер стола: два человека (решают ботом по своему зеркалу) и бот. Партия доходит до конца,
  /// ни одна команда людей не отклонена, партии клиентов совпадают с сервером.
  /// </summary>
  [Test]
  public void WholeGameThroughTable()
  {
    var (net, host, guest) = StartedGame();
    var game = net.Server.Game!;
    var faults = 0;
    game.Faulted += (_, _, _) => faults++;

    for (var step = 0; game.Session.Phase != GamePhase.GameOver; step++)
    {
      Assert.That(step, Is.LessThan(100_000));
      host.Bot.Act();
      guest.Bot.Act();
      net.Pump();
      net.Advance(TimeSpan.FromMilliseconds(20));
    }

    Assert.Multiple(() =>
    {
      Assert.That(faults, Is.Zero);
      Assert.That(host.Bot.Rejections + guest.Bot.Rejections, Is.Zero);
      Assert.That(ViewSerializer.Serialize(host.Client.Game.Mirror!.View), Is.EqualTo(ViewSerializer.Serialize(game.ViewFor(0))));
      Assert.That(ViewSerializer.Serialize(guest.Client.Game.Mirror!.View), Is.EqualTo(ViewSerializer.Serialize(game.ViewFor(1))));
      Assert.That(net.Log, Has.None.Contains("не успел"));
    });
  }

  /// <summary>
  /// Партии с обрывами связи: соединения людей рвутся в случайные моменты (сообщения в пути теряются в обе стороны),
  /// клиенты возвращаются по токену через некоторое время. Партия доходит до конца без сбоев, каждая команда
  /// выполнена не больше одного раза, а в конце партии всех клиентов совпадают с сервером.
  /// </summary>
  [TestCase(GameMode.Money, 501)]
  [TestCase(GameMode.TheoryDevelopment, 502)]
  [TestCase(GameMode.Race, 503)]
  [TestCase(GameMode.Money, 504)]
  public void Chaos_DisconnectsAndReconnects(GameMode mode, int seed)
  {
    var random = new Random(seed);
    var net = new Net(Fast with { TurnTimeout = TimeSpan.FromSeconds(5), DisconnectedTurnTimeout = TimeSpan.FromSeconds(2) });
    var players = new[] { net.Join("Аня"), net.Join("Боря"), net.Join("Вера") };
    players[0].Client.AddBot(BotLevel.Medium);
    players[0].Client.Start(mode, seed % 2 == 0);
    net.Pump();
    var game = net.Server.Game!;
    var faults = 0;
    game.Faulted += (_, _, _) => faults++;
    var accepted = players.Select(_ => new HashSet<long>()).ToArray();
    var stale = 0;
    for (var i = 0; i < players.Length; i++)
    {
      var index = i;
      players[i].Client.Game.Replied += reply =>
      {
        if (reply.Accepted)
          accepted[index].Add(reply.Seq);
        else if (reply.Error?.Contains("Партия изменилась") == true)
          stale++;
      };
    }

    var offlineUntil = new TimeSpan?[players.Length];
    var drops = 0;
    for (var step = 0; game.Session.Phase != GamePhase.GameOver; step++)
    {
      Assert.That(step, Is.LessThan(200_000), "Партия не закончилась.");
      for (var i = 0; i < players.Length; i++)
      {
        if (offlineUntil[i] is { } until)
        {
          if (net.Now >= until)
          {
            offlineUntil[i] = null;
            players[i].Reconnect();
          }
          continue;
        }

        // Обрыв: чаще — сразу после своего хода, когда команда или ответ ещё в пути.
        if (random.Next(players[i].Bot.Act() ? 8 : 400) == 0)
        {
          players[i].Drop();
          drops++;
          offlineUntil[i] = net.Now + TimeSpan.FromMilliseconds(random.Next(50, 4000));
        }
      }

      net.Pump();
      net.Advance(TimeSpan.FromMilliseconds(20));
    }

    // Все возвращаются и получают итог.
    for (var i = 0; i < players.Length; i++)
    {
      if (offlineUntil[i] is not null)
        players[i].Reconnect();
    }
    net.Pump();

    Assert.Multiple(() =>
    {
      Assert.That(faults, Is.Zero);
      for (var i = 0; i < players.Length; i++)
      {
        var name = players[i].Client.Lobby!.Seats[i].Name;
        var auto = net.Log.Count(line => line.Contains($"«{name}» не успел")) - net.Log.Count(line => line.Contains($"не смог походить за «{name}»"));
        var logged = game.Log.Count(entry => entry.Seat == i);
        // Каждая принятая команда — ровно одна запись в журнале; остальное — ходы сервера за игрока.
        Assert.That(logged, Is.EqualTo(accepted[i].Count + auto), $"Игрок {i}: команды выполнены не по одному разу.");
        Assert.That(ViewSerializer.Serialize(players[i].Client.Game.Mirror!.View), Is.EqualTo(ViewSerializer.Serialize(game.ViewFor(i))));
      }
    });
    TestContext.WriteLine($"Обрывов: {drops}, устаревших команд отклонено: {stale}, ходов сервера за людей: {net.Log.Count(line => line.Contains("не успел"))}.");
  }

  /// <summary>
  /// Команда, решённая по старой партии, отклоняется — даже если она верна в новой.
  /// </summary>
  [Test]
  public void StaleCommand_IsRejected()
  {
    var game = ClientMirrorTests.NewGame(2, GameMode.Money, 505, humans: 2);
    var host = new GameHost(game);
    var replies = new List<CommandReply>();
    host.Outgoing += (_, json) =>
    {
      if (ProtocolSerializer.DeserializeServer(json) is CommandReply reply)
        replies.Add(reply);
    };
    var seat = game.ActingSeat!.Value;
    var version = game.Version;
    host.ExecuteForPlayer(seat, new Commands.PassCommand());
    var next = game.ActingSeat!.Value;

    host.Receive(next, ProtocolSerializer.Serialize(new CommandRequest(1, new Commands.PassCommand(), version)));
    Assert.That(replies.Single().Accepted, Is.False);
    host.Receive(next, ProtocolSerializer.Serialize(new CommandRequest(2, new Commands.PassCommand(), game.Version)));
    Assert.That(replies[^1].Accepted, Is.True);
  }

  /// <summary>
  /// Ждём очереди человека (бот ходит быстро) и возвращаем его место.
  /// </summary>
  private static int WaitForHuman(Net net, ServerGame game)
  {
    for (var i = 0; i < 1000 && game.ActingSeat is not (0 or 1); i++)
      net.Advance(TimeSpan.FromMilliseconds(20));
    Assert.That(game.ActingSeat, Is.EqualTo(0).Or.EqualTo(1));
    net.Advance(TimeSpan.FromMilliseconds(20));
    return game.ActingSeat!.Value;
  }

  /// <summary>
  /// Таймер 0 — без ограничения: игрока на связи сервер ждёт сколько угодно, клиенты получают «время не ограничено».
  /// Игрока без связи всё равно долго не ждут, иначе партия встала бы навсегда.
  /// </summary>
  [Test]
  public void TurnTimer_Unlimited_WaitsForConnectedPlayer()
  {
    var (net, host, guest) = StartedGame(Fast with { TurnTimeout = TimeSpan.FromSeconds(30), DisconnectedTurnTimeout = TimeSpan.FromSeconds(5) }, turnSeconds: 0);
    var game = net.Server.Game!;
    var acting = WaitForHuman(net, game);
    var player = game.Session.Players[acting];

    net.Advance(TimeSpan.FromHours(2), ticks: 2000);
    Assert.That(game.Session.Turns.HasPassed(player), Is.False, "Сервер не дождался игрока при неограниченном времени.");
    Assert.That(host.Client.LastTimer, Is.EqualTo((acting, (int?)null)));
    Assert.That(net.Log, Has.Some.Contains("без ограничения"));

    // Связь пропала — ждём недолго.
    (host.Client.Seat == acting ? host : guest).Drop();
    net.Advance(TimeSpan.FromSeconds(6), ticks: 60);
    Assert.That(game.Session.Turns.HasPassed(player) || game.Session.CurrentPlayer != player, Is.True, "Игрока без связи ждали вечно.");
  }

  /// <summary>
  /// Своё время хода из лобби — вместо настроенного на сервере.
  /// </summary>
  [Test]
  public void TurnTimer_CustomLimitFromLobby()
  {
    var (net, host, _) = StartedGame(Fast with { TurnTimeout = TimeSpan.FromSeconds(300) }, turnSeconds: 15);
    var game = net.Server.Game!;
    var acting = WaitForHuman(net, game);
    var player = game.Session.Players[acting];

    net.Advance(TimeSpan.FromSeconds(13), ticks: 130);
    Assert.That(game.Session.Turns.HasPassed(player), Is.False);
    Assert.That(host.Client.LastTimer?.Seconds, Is.InRange(14, 15));
    net.Advance(TimeSpan.FromSeconds(3), ticks: 30);
    Assert.That(game.Session.Turns.HasPassed(player), Is.True);
  }

  [TestCase(-1)]
  [TestCase(3601)]
  public void TurnTimer_OutOfRange_IsRejected(int seconds)
  {
    var net = new Net();
    var host = net.Join("Аня");
    host.Client.AddBot(BotLevel.Easy);
    host.Client.Start(GameMode.Money, true, seconds);
    net.Pump();

    Assert.That(net.Server.Game, Is.Null);
    Assert.That(host.Notices, Has.Some.Contains("Время хода"));
  }
}
