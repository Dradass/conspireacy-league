using ConspiracyLeague.Core.Bots;
using ConspiracyLeague.Core.Client;
using ConspiracyLeague.Core.Commands;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Protocol;
using ConspiracyLeague.Core.Server;
using ConspiracyLeague.Core.Views;

namespace ConspiracyLeague.Core.Tests;

public sealed class ClientProtocolTests
{
  [Test]
  public void ClientMessages_RoundTrip()
  {
    ClientMessage[] messages =
    [
      new CommandRequest(1, new PassCommand()),
      new CommandRequest(42, new PlaceCommand(7, new Field.Cell(3, 4))),
      new SyncRequest(),
    ];
    foreach (var message in messages)
    {
      var json = ProtocolSerializer.Serialize(message);
      Assert.That(ProtocolSerializer.Serialize(ProtocolSerializer.DeserializeClient(json)), Is.EqualTo(json));
    }
  }

  [TestCase("")]
  [TestCase("null")]
  [TestCase("{}")]
  [TestCase("{\"type\":\"command\",\"seq\":1}")]
  [TestCase("{\"type\":\"command\",\"seq\":0,\"command\":{\"type\":\"pass\"}}")]
  [TestCase("{\"type\":\"command\",\"seq\":-5,\"command\":{\"type\":\"pass\"}}")]
  [TestCase("{\"type\":\"command\",\"seq\":1,\"command\":{\"type\":\"pass\"},\"seat\":3}")]
  [TestCase("{\"type\":\"command\",\"seq\":\"1\",\"command\":{\"type\":\"pass\"}}")]
  [TestCase("{\"type\":\"update\",\"update\":null}")]
  public void MalformedClientMessages_AreRejected(string json) =>
    Assert.Throws<FormatException>(() => ProtocolSerializer.DeserializeClient(json));

  [Test]
  public void OversizedClientMessage_IsRejected() =>
    Assert.Throws<FormatException>(() => ProtocolSerializer.DeserializeClient(new string(' ', ProtocolSerializer.MaxClientMessageBytes + 1)));

  /// <summary>
  /// Повтор команды (ответ потерялся) не выполняет её второй раз, а повторяет ответ; старые номера отбрасываются.
  /// </summary>
  [Test]
  public void Host_DeduplicatesCommands()
  {
    var game = ClientMirrorTests.NewGame(2, GameMode.Money, 301, humans: 2);
    var host = new GameHost(game);
    var sent = new List<(int Seat, ServerMessage Message)>();
    host.Outgoing += (seat, json) => sent.Add((seat, ProtocolSerializer.DeserializeServer(json)));
    var seat = game.ActingSeat!.Value;
    var cell = game.Session.Market.OpenCards.Keys.First(cell => game.Session.Market.CheckBuy(cell, game.Session.CurrentPlayer).IsAllowed);
    var buy = ProtocolSerializer.Serialize(new CommandRequest(5, new BuyCommand(cell)));

    host.Receive(seat, buy);
    var version = game.Version;
    var replies = sent.Select(item => item.Message).OfType<CommandReply>().ToList();
    host.Receive(seat, buy);
    host.Receive(seat, ProtocolSerializer.Serialize(new CommandRequest(4, new PassCommand())));

    Assert.Multiple(() =>
    {
      Assert.That(game.Version, Is.EqualTo(version), "Команда выполнилась второй раз.");
      Assert.That(game.Log, Has.Count.EqualTo(1));
      Assert.That(replies.Single(), Is.EqualTo(new CommandReply(5, true, null)));
      Assert.That(sent.Select(item => item.Message).OfType<CommandReply>().ToList(), Is.EqualTo(new[] { replies[0], replies[0] }));
    });
  }

  /// <summary>
  /// Обновление уходит раньше ответа — получив ответ, клиент уже видит последствия команды; обновление получают все люди.
  /// </summary>
  [Test]
  public void Host_SendsUpdatesBeforeReply()
  {
    var game = ClientMirrorTests.NewGame(3, GameMode.Money, 302, humans: 3);
    var host = new GameHost(game);
    var sent = new List<(int Seat, ServerMessage Message)>();
    host.Outgoing += (seat, json) => sent.Add((seat, ProtocolSerializer.DeserializeServer(json)));
    var seat = game.ActingSeat!.Value;

    host.Receive(seat, ProtocolSerializer.Serialize(new CommandRequest(1, new PassCommand())));

    Assert.That(sent.Select(item => (item.Seat, item.Message.GetType())), Is.EqualTo(new[]
    {
      (0, typeof(UpdateMessage)),
      (1, typeof(UpdateMessage)),
      (2, typeof(UpdateMessage)),
      (seat, typeof(CommandReply)),
    }));
    Assert.That(sent.Take(3).Select(item => ((UpdateMessage)item.Message).Update.View.Viewer), Is.EqualTo(new int?[] { 0, 1, 2 }));
  }

  [Test]
  public void Host_AnswersGarbageWithoutChangingGame()
  {
    var game = ClientMirrorTests.NewGame(2, GameMode.Money, 303, humans: 2);
    var host = new GameHost(game);
    var sent = new List<ServerMessage>();
    host.Outgoing += (_, json) => sent.Add(ProtocolSerializer.DeserializeServer(json));

    host.Receive(0, "{\"type\":\"command\",\"seq\":1,\"command\":{\"type\":\"hack\"}}");
    host.Receive(0, new string('[', 5000));

    Assert.That(game.Version, Is.Zero);
    Assert.That(sent, Is.EqualTo(new ServerMessage[]
    {
      new CommandReply(0, false, "Сервер не смог разобрать сообщение."),
      new CommandReply(0, false, "Сервер не смог разобрать сообщение."),
    }));
  }

  /// <summary>
  /// Пока сервер не ответил, вторая команда не уходит; старое обновление не откатывает партию клиента.
  /// </summary>
  [Test]
  public void Client_SendsOneCommandAtATime_AndIgnoresStaleUpdates()
  {
    var game = ClientMirrorTests.NewGame(2, GameMode.Money, 304, humans: 2);
    var outbox = new List<string>();
    var client = new GameClient(outbox.Add);
    Assert.That(client.Send(new PassCommand()), Is.False, "Без партии команда не уходит.");

    var stale = ProtocolSerializer.Serialize(new UpdateMessage(game.UpdateFor(0, [])));
    client.Receive(stale);
    Assert.That(client.Send(new PassCommand()), Is.True);
    Assert.That(client.Send(new PassCommand()), Is.False);
    Assert.That(outbox, Has.Count.EqualTo(1));

    game.Execute(game.ActingSeat!.Value, new PassCommand());
    client.Receive(ProtocolSerializer.Serialize(new UpdateMessage(game.UpdateFor(0, []))));
    var version = client.Mirror!.View.Version;
    client.Receive(stale);

    Assert.That(client.Mirror.View.Version, Is.EqualTo(version));
    client.Receive(ProtocolSerializer.Serialize(new CommandReply(1, true, null)));
    Assert.That(client.IsWaiting, Is.False);
  }

  /// <summary>
  /// Показ (бросок кубика) придерживает обновление: партия меняется только после него, а пришедшее следом
  /// (ещё обновление, ответ на команду) применяется по порядку.
  /// </summary>
  [Test]
  public void Client_HoldsUpdatesWhilePaused()
  {
    var game = ClientMirrorTests.NewGame(2, GameMode.Money, 305, humans: 2);
    var client = new GameClient(_ => { });
    client.Receive(ProtocolSerializer.Serialize(new UpdateMessage(game.UpdateFor(0, []))));
    var start = client.Mirror!.View.Version;

    var arrived = new List<long>();
    var log = new List<string>();
    client.Arriving += update =>
    {
      arrived.Add(update.View.Version);
      Assert.That(client.Mirror.View.Version, Is.LessThan(update.View.Version), "Пока показ идёт, партия прежняя.");
      if (arrived.Count == 1)
        client.Pause();
    };
    client.Updated += update => log.Add($"update {update.View.Version}");
    client.Replied += reply => log.Add($"reply {reply.Seq}");
    Assert.That(client.Send(new PassCommand()), Is.True);

    game.Execute(game.ActingSeat!.Value, new PassCommand());
    var first = game.UpdateFor(0, []);
    client.Receive(ProtocolSerializer.Serialize(new UpdateMessage(first)));
    game.Execute(game.ActingSeat!.Value, new PassCommand());
    client.Receive(ProtocolSerializer.Serialize(new UpdateMessage(game.UpdateFor(0, []))));
    client.Receive(ProtocolSerializer.Serialize(new CommandReply(1, true, null)));

    Assert.That(client.Mirror.View.Version, Is.EqualTo(start));
    Assert.That(client.IsWaiting, Is.True, "Ответ не обгоняет придержанное обновление.");
    Assert.That(log, Is.Empty);

    client.Resume();

    Assert.That(arrived, Is.EqualTo(new[] { first.View.Version, game.Version }));
    Assert.That(log, Is.EqualTo(new[] { $"update {first.View.Version}", $"update {game.Version}", "reply 1" }));
    Assert.That(client.Mirror.View.Version, Is.EqualTo(game.Version));
    Assert.That(client.IsWaiting, Is.False);
  }

  [Test]
  public void Client_ReportsBrokenServerMessages()
  {
    var client = new GameClient(_ => { });
    var errors = new List<string>();
    client.ProtocolError += errors.Add;

    client.Receive("{\"type\":\"update\",\"update\":{\"version\":1,\"events\":[],\"view\":null}}");
    client.Receive("{\"type\":\"surprise\"}");

    Assert.That(errors, Has.Count.EqualTo(2));
    Assert.That(client.Mirror, Is.Null);
  }

  /// <summary>
  /// Партия целиком через протокол: люди за столом — клиенты, которые видят только свою партию (зеркало), выбирают ходы
  /// ботом по зеркалу и отправляют команды в JSON; места-боты ведёт сервер. Партия доходит до конца, а партии клиентов
  /// в конце совпадают с видом сервера.
  /// </summary>
  [TestCase(GameMode.Money, 3, 2, 311)]
  [TestCase(GameMode.TheoryDevelopment, 4, 4, 312)]
  [TestCase(GameMode.Race, 5, 2, 313)]
  public void WholeGameThroughProtocol(GameMode mode, int players, int humans, int seed)
  {
    var game = ClientMirrorTests.NewGame(players, mode, seed, humans);
    var faults = 0;
    game.Faulted += (_, _, _) => faults++;
    var link = new LocalLink(new GameHost(game));
    var clients = Enumerable.Range(0, humans).Select(seat => new ClientPlayer(link.Connect(seat), seat)).ToList();
    var errors = new List<string>();
    foreach (var client in clients)
      client.Client.ProtocolError += errors.Add;

    for (var step = 0; game.Session.Phase != GamePhase.GameOver; step++)
    {
      Assert.That(step, Is.LessThan(30_000), "Партия не закончилась.");
      if (link.RunBotStep())
        continue;
      var acting = game.ActingSeat;
      Assert.That(acting, Is.Not.Null.And.LessThan(humans), "Очередь человека, но сервер ничего не ждёт.");
      clients[acting!.Value].Act();
    }

    Assert.Multiple(() =>
    {
      Assert.That(faults, Is.Zero);
      Assert.That(errors, Is.Empty);
      foreach (var client in clients)
      {
        Assert.That(ViewSerializer.Serialize(client.Client.Mirror!.View), Is.EqualTo(ViewSerializer.Serialize(game.ViewFor(client.Seat))));
        Assert.That(client.Client.IsWaiting, Is.False);
      }
    });
    TestContext.WriteLine($"Отказов клиентам: {string.Join(", ", clients.Select(client => client.Rejections))}.");
  }

  /// <summary>
  /// Игрок-человек в тесте: решает ботом по своей партии-зеркалу и ходит только командами.
  /// </summary>
  private sealed class ClientPlayer
  {
    private const int MaxFailures = 5;
    private readonly HashSet<string> _failed = [];
    private long _version = -1;
    private CommandReply? _reply;

    public ClientPlayer(GameClient client, int seat)
    {
      Client = client;
      Seat = seat;
      client.Replied += reply => _reply = reply;
    }

    public GameClient Client { get; }

    public int Seat { get; }

    public int Rejections { get; private set; }

    public void Act()
    {
      var mirror = Client.Mirror!;
      var session = mirror.Session;
      if (mirror.View.Version != _version)
        _failed.Clear();
      _version = mirror.View.Version;
      var bot = new Bot(mirror.Player!);

      if (session.PendingTheoryTrigger is { } trigger)
      {
        foreach (var command in CommandEncoder.FromTrigger(session, bot.ChooseTrigger(session, trigger)).ToList())
        {
          if (!Send(command))
            break;
        }
        if (Client.Mirror!.Session.PendingTheoryTrigger is not null && Client.Mirror.View.PendingTrigger?.OwnerSeat == Seat)
          Send(new SkipTheoryTriggerCommand());
        return;
      }

      if (session.PendingPlacement is { } choice)
      {
        if (Send(CommandEncoder.FromPlacement(bot.ChoosePlacement(session, choice))))
          return;
        // Подходящие цели клиент берёт из вида: по зеркалу их не посчитать, если решает скрытое.
        var placement = Client.Mirror!.View.PendingPlacement!;
        Send(new ResolvePlacementCommand(placement.TargetIsOptional ? null : placement.AllowedTargets?.FirstOrDefault()));
        return;
      }

      if (_failed.Count >= MaxFailures)
      {
        Send(new PassCommand());
        return;
      }

      var move = bot.ChooseMove(session, _failed);
      if (!Send(CommandEncoder.FromMove(session, move)))
        _failed.Add(move is PassMove ? "pass" : move.Key);
    }

    private bool Send(GameCommand command)
    {
      _reply = null;
      Assert.That(Client.Send(command), Is.True);
      Assert.That(_reply, Is.Not.Null, "Сервер не ответил.");
      if (!_reply!.Accepted)
        Rejections++;
      return _reply.Accepted;
    }
  }
}
