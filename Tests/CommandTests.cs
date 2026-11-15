using ConspiracyLeague.Core.Commands;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Tests;

public sealed class CommandSerializerTests
{
  private static IEnumerable<GameCommand> AllCommands() =>
  [
    new BuyCommand(new Cell(4, 45)),
    new PlaceCommand(123, new Cell(1, 2)),
    new PlaceInWallCommand(7, 12),
    new PlaceHiddenCommand(9, "Плоская Земля"),
    new ResolvePlacementCommand(),
    new ResolvePlacementCommand(new Cell(1, 1), new Cell(2, 2)),
    new UseCardCommand(new Cell(3, 30), 1, new CommandTargets
    {
      Theories = ["А", "Б"],
      Theory = "Б",
      SourceTheory = "В",
      HandCard = 5,
      TargetCell = new Cell(1, 1),
      SecondTargetCell = new Cell(1, 2),
      AdeptCard = new Cell(2, 3),
      TargetPlayer = 2,
      BorrowedCell = new Cell(5, 5),
      BorrowedOption = 0,
      CopiedCell = new Cell(6, 6),
      CopiedOption = 1,
    }),
    new UseTheoryCardCommand(77, CommandTargets.None),
    new SelectTriggerHandCardCommand(8),
    new SelectTriggerTheoryCommand("Рептилоиды"),
    new SelectTriggerCellCommand(new Cell(0, 0)),
    new SkipTheoryTriggerCommand(),
    new PassCommand(),
  ];

  [TestCaseSource(nameof(AllCommands))]
  public void Command_SurvivesJsonRoundTrip(GameCommand command)
  {
    var json = CommandSerializer.Serialize(command);
    var parsed = CommandSerializer.Deserialize(json);

    Assert.That(parsed.GetType(), Is.EqualTo(command.GetType()));
    Assert.That(CommandSerializer.Serialize(parsed), Is.EqualTo(json));
  }

  [TestCase("""{"type":"pass"}""", typeof(PassCommand))]
  [TestCase("""{"type":"buy","cell":{"column":4,"row":45}}""", typeof(BuyCommand))]
  public void Deserialize_ReadsWireFormat(string json, Type type) =>
    Assert.That(CommandSerializer.Deserialize(json), Is.InstanceOf(type));

  [TestCase("")]
  [TestCase("null")]
  [TestCase("42")]
  [TestCase("[]")]
  [TestCase("{}")]
  [TestCase("""{"type":"format c:"}""")]
  [TestCase("""{"type":"pass","extra":1}""")]
  [TestCase("""{"type":"buy","cell":{"column":"4","row":45}}""")]
  [TestCase("""{"type":"buy","cell":{"column":4.5,"row":45}}""")]
  [TestCase("""{"type":"buy","cell":{"column":99999999999,"row":45}}""")]
  [TestCase("""{"type":"pass",}""")]
  [TestCase("""{"type":"pass" /* комментарий */}""")]
  [TestCase("""{"cell":{"column":4,"row":45},"type":"buy"}""")]
  [TestCase("""{"type":"useCard","cell":{"column":1,"row":1},"option":0,"targets":{"theories":[[[[[[[[[[1]]]]]]]]]]}}""")]
  public void Deserialize_RejectsMalformedMessages(string json) =>
    Assert.Throws<CommandFormatException>(() => CommandSerializer.Deserialize(json));

  [Test]
  public void Deserialize_RejectsOversizedMessages()
  {
    var json = $$"""{"type":"placeHidden","cardId":1,"theory":"{{new string('я', CommandSerializer.MaxMessageBytes)}}"}""";
    Assert.Throws<CommandFormatException>(() => CommandSerializer.Deserialize(json));
  }
}

public sealed class CardRegistryTests
{
  [Test]
  public void IdOf_IsStableAndUniquePerCardInstance()
  {
    var registry = new CardRegistry();
    var first = new Cards.CitizenCard();
    var second = new Cards.CitizenCard();

    var id = registry.IdOf(first);

    Assert.That(registry.IdOf(first), Is.EqualTo(id));
    Assert.That(registry.IdOf(second), Is.Not.EqualTo(id));
    Assert.That(registry.Find(id), Is.SameAs(first));
    Assert.That(registry.Find(0), Is.Null, "Ноль не выдаётся.");
  }
}

public sealed class CommandProcessorTests
{
  private static GameSession StartedGame(int seed = 1)
  {
    var session = new GameSession(
      Enum.GetValues<PlayerColor>().Take(3).Select((color, index) => new Player($"Игрок {index + 1}", color, GameSession.StartingMoney)),
      new Random(seed));
    session.Start();
    return session;
  }

  [Test]
  public void Execute_RejectsCommandFromPlayerWhoseTurnItIsNot()
  {
    var session = StartedGame();
    var processor = new CommandProcessor(session);
    var other = session.Players.ToList().FindIndex(player => player != session.CurrentPlayer);
    var before = GameStateDump.ToJson(session);

    var result = processor.Execute(other, new PassCommand());

    Assert.That(result.Accepted, Is.False);
    Assert.That(GameStateDump.ToJson(session), Is.EqualTo(before));
  }

  [TestCase(-1)]
  [TestCase(3)]
  [TestCase(int.MaxValue)]
  public void Execute_RejectsUnknownSeat(int seat)
  {
    var session = StartedGame();
    Assert.That(new CommandProcessor(session).Execute(seat, new PassCommand()).Accepted, Is.False);
  }

  [Test]
  public void Execute_RejectsCardFromAnotherPlayersHand()
  {
    var session = StartedGame();
    var processor = new CommandProcessor(session);
    var seat = session.Players.ToList().IndexOf(session.CurrentPlayer);
    var rival = session.Players.First(player => player != session.CurrentPlayer);
    var rivalCard = session.CardIds.IdOf(rival.Hand.Cards[0]);
    var before = GameStateDump.ToJson(session);

    var result = processor.Execute(seat, new PlaceCommand(rivalCard, session.Field.PlayerZones[session.CurrentPlayer].Cells.First()));

    Assert.That(result.Accepted, Is.False);
    Assert.That(GameStateDump.ToJson(session), Is.EqualTo(before));
  }

  [Test]
  public void Execute_RejectsUnknownCardId()
  {
    var session = StartedGame();
    var seat = session.Players.ToList().IndexOf(session.CurrentPlayer);
    var result = new CommandProcessor(session).Execute(seat, new PlaceCommand(12345, new Cell(0, 0)));
    Assert.That(result.Accepted, Is.False);
  }

  [Test]
  public void Execute_RejectsTriggerChoiceWhenNothingIsPending()
  {
    var session = StartedGame();
    var seat = session.Players.ToList().IndexOf(session.CurrentPlayer);
    Assert.That(new CommandProcessor(session).Execute(seat, new SkipTheoryTriggerCommand()).Accepted, Is.False);
  }

  [Test]
  public void Execute_AcceptsLegalBuyAndSpendsAction()
  {
    var session = StartedGame();
    var processor = new CommandProcessor(session);
    var seat = session.Players.ToList().IndexOf(session.CurrentPlayer);
    var cell = session.Market.OpenCards.Keys.First(cell => session.Market.CheckBuy(cell, session.CurrentPlayer).IsAllowed);
    var actions = session.ActionsLeft;

    var result = processor.Execute(seat, new BuyCommand(cell));

    Assert.That(result.Accepted, Is.True, result.Error);
    Assert.That(session.ActionsLeft, Is.EqualTo(actions - 1));
  }
}

public sealed class NetworkBotGameTests
{
  private static IEnumerable<TestCaseData> Games()
  {
    var seed = 0;
    foreach (var mode in Enum.GetValues<GameMode>())
    {
      foreach (var players in new[] { 2, 3, 4 })
        yield return new TestCaseData(mode, players, ++seed).SetName($"{mode}, {players} игрока, seed {seed}");
    }
  }

  /// <summary>
  /// Партии ботов целиком через команды и JSON: каждая партия доходит до конца, ни одна команда не падает с ошибкой,
  /// а отклонённые команды не меняют партию.
  /// </summary>
  [TestCaseSource(nameof(Games))]
  public void BotsFinishGameThroughCommands(GameMode mode, int players, int seed)
  {
    var game = new NetworkBotGame(players, mode, seed);

    game.Play();

    TestContext.WriteLine($"Команд выполнено: {game.Accepted}, отклонено: {game.Rejections.Count}.");
    foreach (var rejection in game.Rejections.Take(20))
      TestContext.WriteLine($"  отклонена {rejection}");
    Assert.Multiple(() =>
    {
      Assert.That(game.Session.Phase, Is.EqualTo(GamePhase.GameOver));
      Assert.That(game.Faults, Is.Empty, "Команды упали с ошибкой в правилах.");
      Assert.That(game.DirtyRejections, Is.Empty, "Отклонённые команды изменили партию.");
    });
  }

  /// <summary>
  /// Посреди партии ботов сервер забрасывают случайными и мусорными командами от всех игроков: сервер не падает,
  /// ни одна команда не ломает правила, отклонённые команды партию не меняют, и партия всё равно доходит до конца.
  /// </summary>
  [TestCase(101)]
  [TestCase(202)]
  [TestCase(303)]
  [TestCase(404)]
  [TestCase(505)]
  [TestCase(606)]
  public void ServerSurvivesRandomCommands(int seed)
  {
    var random = new Random(seed);
    var game = new NetworkBotGame(3, GameMode.TheoryDevelopment, seed)
    {
      BeforeStep = game =>
      {
        for (var i = 0; i < 3; i++)
        {
          var seat = random.Next(10) == 0 ? random.Next(-1, 5) : game.Server.SeatOf(game.Session.CurrentPlayer);
          var command = RandomCommand(game.Session, random);
          // Пас и любые выполненные случайные ходы — законные ходы игрока; они просто меняют ход партии.
          if (command is PassCommand)
            continue;
          if (seat >= 0 && seat < game.Session.Players.Count)
            game.Send(seat, command);
          else
            Assert.That(game.Server.Execute(seat, command).Accepted, Is.False);
          if (game.Session.Phase != GamePhase.PlayerTurns)
            return;
        }
      },
    };

    game.Play();

    TestContext.WriteLine($"Команд выполнено: {game.Accepted}, отклонено: {game.Rejections.Count}.");
    foreach (var reason in game.Rejections.Select(rejection => rejection[(rejection.IndexOf("}: ", StringComparison.Ordinal) + 3)..])
      .GroupBy(reason => reason).OrderByDescending(group => group.Count()).Take(15))
      TestContext.WriteLine($"  {reason.Count()} × {reason.Key}");
    Assert.Multiple(() =>
    {
      Assert.That(game.Session.Phase, Is.EqualTo(GamePhase.GameOver));
      Assert.That(game.Faults, Is.Empty, "Команды упали с ошибкой в правилах.");
      Assert.That(game.DirtyRejections, Is.Empty, "Отклонённые команды изменили партию.");
    });
  }

  private static GameCommand RandomCommand(GameSession session, Random random)
  {
    var cards = session.Players.SelectMany(player => player.Hand.Cards).Select(session.CardIds.IdOf)
      .Concat(session.Field.TheoryCards.Select(owned => session.CardIds.IdOf(owned.Card)))
      .Append(random.Next())
      .ToList();
    var theories = session.Field.TheoryColumns.Select(column => column.Theory.Name).Append("Нет такой теории").ToList();
    var occupied = session.Field.PlayerZones.Values.SelectMany(zone => zone.Cells)
      .Concat(session.Field.TheoryColumns.SelectMany(column => column.Cells))
      .Concat(session.Market.OpenCards.Keys)
      .ToList();

    int Card() => cards[random.Next(cards.Count)];
    string Theory() => theories[random.Next(theories.Count)];
    Cell AnyCell() => random.Next(4) == 0
      ? new Cell(random.Next(-2, FieldLayout.Columns + 2), random.Next(-2, FieldLayout.Rows + 2))
      : occupied[random.Next(occupied.Count)];
    Cell? MaybeCell() => random.Next(2) == 0 ? null : AnyCell();
    int? MaybeOption() => random.Next(2) == 0 ? null : random.Next(-1, 4);

    // Своя несыгранная карточка — так случайные цели доходят до проверки целей розыгрыша, а не отсекаются раньше.
    Cell? OwnUsableCell()
    {
      var usable = session.Field.PlayerZones[session.CurrentPlayer].Cells.Where(cell => session.Field.CanUse(cell, session.CurrentPlayer)).ToList();
      return usable.Count == 0 ? null : usable[random.Next(usable.Count)];
    }

    CommandTargets Targets() => new()
    {
      Theories = random.Next(4) == 0 ? Enumerable.Range(0, random.Next(0, 4)).Select(_ => Theory()).ToList() : null,
      Theory = random.Next(2) == 0 ? Theory() : null,
      SourceTheory = random.Next(4) == 0 ? Theory() : null,
      HandCard = random.Next(3) == 0 ? Card() : null,
      TargetCell = MaybeCell(),
      SecondTargetCell = random.Next(4) == 0 ? AnyCell() : null,
      AdeptCard = random.Next(6) == 0 ? AnyCell() : null,
      TargetPlayer = random.Next(4) == 0 ? random.Next(-1, 5) : null,
      BorrowedCell = random.Next(4) == 0 ? AnyCell() : null,
      BorrowedOption = MaybeOption(),
      CopiedCell = random.Next(6) == 0 ? AnyCell() : null,
      CopiedOption = MaybeOption(),
    };

    return random.Next(12) switch
    {
      0 => new BuyCommand(AnyCell()),
      1 => new PlaceCommand(Card(), AnyCell()),
      2 => new PlaceInWallCommand(Card(), random.Next(-2, FieldLayout.Columns + 2)),
      3 => new PlaceHiddenCommand(Card(), Theory()),
      4 => new ResolvePlacementCommand(MaybeCell(), MaybeCell()),
      5 => new UseCardCommand(AnyCell(), random.Next(-1, 4), Targets()),
      6 when OwnUsableCell() is { } own => new UseCardCommand(own, random.Next(session.Field.CardAt(own)!.OnPlayFromHand.Options.Count), Targets()),
      6 or 7 => new UseTheoryCardCommand(Card(), Targets()),
      8 => new SelectTriggerHandCardCommand(Card()),
      9 => new SelectTriggerTheoryCommand(Theory()),
      10 => new SelectTriggerCellCommand(AnyCell()),
      _ => new SkipTheoryTriggerCommand(),
    };
  }
}
