using ConspiracyLeague.Core.Bots;
using ConspiracyLeague.Core.Commands;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Server;
using ConspiracyLeague.Core.Views;

namespace ConspiracyLeague.Core.Tests;

public sealed class ServerBotsTests
{
  private static ServerGame NewGame(int players, GameMode mode, int seed, int humans = 0)
  {
    var levels = Enum.GetValues<BotLevel>();
    return new ServerGame(new GameSetup(
      Enum.GetValues<PlayerColor>().Take(players)
        .Select((color, index) => new SeatSetup($"Игрок {index + 1}", color, IsBot: index >= humans, levels[(seed + index) % levels.Length]))
        .ToList(),
      mode,
      FailureConsequences: true,
      NetworkBotGame.KeyFrom(seed)));
  }

  /// <summary>
  /// Партия одних ботов целиком на сервере: сервер сам ходит за ботов и проводит раунды, ни одного сбоя,
  /// а события и вид партии каждый раз переживают JSON.
  /// </summary>
  [TestCase(GameMode.Money, 2, 81)]
  [TestCase(GameMode.Money, 4, 82)]
  [TestCase(GameMode.TheoryDevelopment, 3, 83)]
  [TestCase(GameMode.Race, 4, 84)]
  public void BotsPlayWholeGameOnServer(GameMode mode, int players, int seed)
  {
    var game = NewGame(players, mode, seed);
    var faults = new List<Exception>();
    game.Faulted += (_, _, error) => faults.Add(error);
    var events = new List<TableEvent>();

    for (var step = 0; step < 20_000 && game.RunBotStep(); step++)
    {
      var drained = game.DrainEvents();
      events.AddRange(drained);
      if (step % 10 == 0)
      {
        var json = ViewSerializer.Serialize(game.UpdateFor(step % players, drained));
        Assert.That(ViewSerializer.Serialize(ViewSerializer.DeserializeUpdate(json)), Is.EqualTo(json));
      }
    }

    TestContext.WriteLine($"Событий: {events.Count}; виды: {string.Join(", ", events.GroupBy(e => e.GetType().Name).OrderByDescending(g => g.Count()).Take(12).Select(g => $"{g.Key} {g.Count()}"))}.");
    Assert.Multiple(() =>
    {
      Assert.That(game.Session.Phase, Is.EqualTo(GamePhase.GameOver));
      Assert.That(game.BotsStuck, Is.False);
      Assert.That(faults, Is.Empty);
      Assert.That(events.OfType<TurnStarted>(), Is.Not.Empty);
      Assert.That(events.OfType<DiceRolled>(), Is.Not.Empty);
      Assert.That(events.OfType<CardBought>(), Is.Not.Empty);
      Assert.That(events.OfType<PhaseChanged>().Last().Phase, Is.EqualTo(GamePhase.GameOver));
    });
  }

  /// <summary>
  /// Деньги в событиях сходятся с видом партии: события описывают все изменения денег, по порядку.
  /// </summary>
  [Test]
  public void MoneyEvents_MatchView()
  {
    var game = NewGame(3, GameMode.Money, 91);
    var money = game.Session.Players.Select(player => player.Money).ToArray();
    game.DrainEvents();
    for (var step = 0; step < 20_000 && game.RunBotStep(); step++)
    {
      foreach (var changed in game.DrainEvents().OfType<MoneyChanged>())
        money[changed.Seat] = changed.Money;
      Assert.That(money, Is.EqualTo(game.ViewFor(null).Players.Select(player => player.Money)), $"Шаг {step}.");
    }
  }

  /// <summary>
  /// За стол сели люди и боты: сервер ходит только за ботов и останавливается, когда очередь человека.
  /// </summary>
  [Test]
  public void BotsNeverActForHumans()
  {
    var game = NewGame(3, GameMode.TheoryDevelopment, 92, humans: 1);
    var humanCommands = 0;
    for (var step = 0; step < 20_000 && game.Session.Phase != GamePhase.GameOver; step++)
    {
      game.RunBots();
      if (game.ActingSeat is not 0)
        continue;

      // Человек: выбирает, если от него ждут выбора, иначе пасует.
      var session = game.Session;
      GameCommand command = session.PendingTheoryTrigger is not null ? new SkipTheoryTriggerCommand()
        : session.PendingPlacement is { } choice ? new ResolvePlacementCommand(
          choice.Effect.TargetIsOptional ? null
          : choice.Effect.TargetCells(session.PlacementContext(choice)).First(target => target.Check.IsAllowed).Cell)
        : new PassCommand();
      Assert.That(game.Execute(0, command).Accepted, Is.True);
      humanCommands++;
    }

    Assert.That(game.Session.Phase, Is.EqualTo(GamePhase.GameOver));
    Assert.That(game.Log.Count(entry => entry.Seat == 0), Is.EqualTo(humanCommands), "Сервер сходил за человека.");
    Assert.That(humanCommands, Is.GreaterThan(0));
  }

  [Test]
  public void RunBots_DoesNothingOnHumanTurn()
  {
    var game = NewGame(2, GameMode.Money, 93, humans: 2);
    Assert.That(game.RunBots(), Is.Zero);
    Assert.That(game.Version, Is.Zero);
  }

  /// <summary>
  /// После сбоя клиенты получают не события отменённой команды, а сигнал показать новый вид.
  /// </summary>
  [Test]
  public void Fault_ReplacesEventsWithStateReset()
  {
    var game = NewGame(2, GameMode.Money, 94, humans: 2);
    var seat = game.ActingSeat!.Value;
    var cell = game.Session.Market.OpenCards.Keys.First(cell => game.Session.Market.CheckBuy(cell, game.Session.CurrentPlayer).IsAllowed);
    game.DrainEvents();
    game.FaultInjector = _ => throw new InvalidOperationException("Сбой.");

    game.Execute(seat, new BuyCommand(cell));

    Assert.That(game.DrainEvents(), Is.EqualTo(new TableEvent[] { new StateReset() }));
  }

  [Test]
  public void AcceptedCommand_ProducesIntentAndRuleEvents()
  {
    var game = NewGame(2, GameMode.Money, 95, humans: 2);
    var seat = game.ActingSeat!.Value;
    var cell = game.Session.Market.OpenCards.Keys.First(cell => game.Session.Market.CheckBuy(cell, game.Session.CurrentPlayer).IsAllowed);
    game.DrainEvents();

    Assert.That(game.Execute(seat, new BuyCommand(cell)).Accepted, Is.True);
    var events = game.DrainEvents();

    Assert.That(events[0], Is.EqualTo(new CardBought(seat, cell)));
    Assert.That(events, Has.Some.EqualTo(new MarketCardTaken(cell)));
    Assert.That(events.OfType<MoneyChanged>().Single().Seat, Is.EqualTo(seat));
    Assert.That(events.OfType<HandCountChanged>().Single().Count, Is.EqualTo(game.Session.Players[seat].Hand.Cards.Count));
  }

  /// <summary>
  /// В событиях нет карточек и их идентификаторов — скрытому там взяться неоткуда.
  /// </summary>
  [Test]
  public void Events_CarryNoCards()
  {
    var game = NewGame(4, GameMode.Race, 96);
    game.RunBots();
    var events = game.DrainEvents();
    var json = ViewSerializer.Serialize(new ServerUpdate(0, events, game.ViewFor(null)));
    var eventsJson = json[..json.IndexOf("\"view\"", StringComparison.Ordinal)];

    Assert.That(events, Is.Not.Empty);
    Assert.That(eventsJson, Does.Not.Contain("\"id\""));
    Assert.That(eventsJson, Does.Not.Contain("\"face\""));
  }
}
