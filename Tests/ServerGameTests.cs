using ConspiracyLeague.Core.Commands;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Server;

namespace ConspiracyLeague.Core.Tests;

public sealed class SecureSeededRandomTests
{
  [Test]
  public void SameKey_GivesSameSequence()
  {
    var key = SecureSeededRandom.NewKey();
    var first = new SecureSeededRandom(key);
    var second = new SecureSeededRandom(key);

    var a = Enumerable.Range(0, 1000).Select(_ => first.Next(1, 21)).ToList();
    var b = Enumerable.Range(0, 1000).Select(_ => second.Next(1, 21)).ToList();

    Assert.That(a, Is.EqualTo(b));
  }

  [Test]
  public void DifferentKeys_GiveDifferentSequences()
  {
    var a = new SecureSeededRandom(SecureSeededRandom.NewKey());
    var b = new SecureSeededRandom(SecureSeededRandom.NewKey());
    Assert.That(Enumerable.Range(0, 64).Select(_ => a.Next()), Is.Not.EqualTo(Enumerable.Range(0, 64).Select(_ => b.Next())));
  }

  [Test]
  public void Next_StaysInRangeAndCoversIt()
  {
    var random = new SecureSeededRandom(SecureSeededRandom.NewKey());
    var counts = new int[20];
    for (var i = 0; i < 20_000; i++)
    {
      var die = random.Next(1, 21);
      Assert.That(die, Is.InRange(1, 20));
      counts[die - 1]++;
    }

    // Каждая грань d20 — около 1000 раз из 20 000; 700 — с огромным запасом на случайность.
    Assert.That(counts, Has.All.GreaterThan(700));
  }

  [Test]
  public void Shuffle_IsReproducible()
  {
    var key = SecureSeededRandom.NewKey();
    var a = Enumerable.Range(0, 50).ToArray();
    var b = Enumerable.Range(0, 50).ToArray();

    new SecureSeededRandom(key).Shuffle(a);
    new SecureSeededRandom(key).Shuffle(b);

    Assert.That(a, Is.EqualTo(b));
    Assert.That(a, Is.Not.EqualTo(Enumerable.Range(0, 50)));
  }

  [Test]
  public void WrongKeyLength_IsRejected() =>
    Assert.Throws<ArgumentException>(() => _ = new SecureSeededRandom(new byte[16]));
}

public sealed class ServerGameTests
{
  /// <summary>
  /// Партия детерминирована по ключу: собранная заново из журнала, она совпадает с живой. На этом держится откат.
  /// </summary>
  [TestCase(GameMode.Money, 3, 11)]
  [TestCase(GameMode.TheoryDevelopment, 4, 12)]
  [TestCase(GameMode.Race, 2, 13)]
  public void Replay_ReproducesFinishedGame(GameMode mode, int players, int seed)
  {
    var game = new NetworkBotGame(players, mode, seed, checkRejectedAreClean: false);
    game.Play();

    var replayed = game.Server.Replay();

    Assert.That(GameStateDump.ToJson(replayed), Is.EqualTo(GameStateDump.ToJson(game.Session)));
  }

  [Test]
  public void Replay_ReproducesGameInTheMiddle()
  {
    var steps = 0;
    var checkedStates = 0;
    var playing = new NetworkBotGame(3, GameMode.TheoryDevelopment, 21, checkRejectedAreClean: false)
    {
      BeforeStep = current =>
      {
        if (++steps % 25 != 0)
          return;
        Assert.That(GameStateDump.ToJson(current.Server.Replay()), Is.EqualTo(GameStateDump.ToJson(current.Session)), $"Шаг {steps}.");
        checkedStates++;
      },
    };

    playing.Play();

    Assert.That(checkedStates, Is.GreaterThan(5));
  }

  [Test]
  public void SameKey_GivesSameGame_DifferentKey_DifferentDeal()
  {
    var seats = new[] { new SeatSetup("А", PlayerColor.Red), new SeatSetup("Б", PlayerColor.Blue) };
    var key = NetworkBotGame.KeyFrom(5);
    var a = new ServerGame(new GameSetup(seats, GameMode.Money, true, key));
    var b = new ServerGame(new GameSetup(seats, GameMode.Money, true, key));
    var c = new ServerGame(new GameSetup(seats, GameMode.Money, true, NetworkBotGame.KeyFrom(6)));

    Assert.That(GameStateDump.ToJson(a.Session), Is.EqualTo(GameStateDump.ToJson(b.Session)));
    Assert.That(GameStateDump.ToJson(a.Session), Is.Not.EqualTo(GameStateDump.ToJson(c.Session)));
  }

  /// <summary>
  /// Команда упала посреди выполнения — сервер возвращает партию к состоянию до неё, и партия продолжается.
  /// </summary>
  [Test]
  public void FaultMidCommand_RollsBackAndGameContinues()
  {
    var injected = 0;
    var rolledBack = 0;
    var unchanged = 0;
    var steps = 0;
    var wrapped = new NetworkBotGame(3, GameMode.TheoryDevelopment, 31, checkRejectedAreClean: false)
    {
      BeforeStep = current =>
      {
        // Каждый 7-й шаг: законная команда (покупка или пас), но после её выполнения «ломаемся».
        if (++steps % 7 != 0 || current.Session.CheckAction() is { IsAllowed: false })
          return;
        var session = current.Session;
        var seat = current.Server.SeatOf(session.CurrentPlayer);
        GameCommand command = session.Market.OpenCards.Keys
          .Where(cell => session.Market.CheckBuy(cell, session.CurrentPlayer).IsAllowed)
          .Select(cell => (GameCommand?)new BuyCommand(cell))
          .FirstOrDefault() ?? new PassCommand();

        var before = GameStateDump.ToJson(session);
        var version = current.Server.Version;
        var log = current.Server.Log.Count;
        current.Server.FaultInjector = changed =>
        {
          // Assert здесь не годится: его исключение перехватил бы сам откат.
          if (GameStateDump.ToJson(changed) == before)
            unchanged++;
          throw new InvalidOperationException("Сбой посреди команды.");
        };
        var result = current.Server.Execute(seat, command);
        current.Server.FaultInjector = null;
        injected++;

        Assert.Multiple(() =>
        {
          Assert.That(result.Accepted, Is.False);
          Assert.That(result.Fault, Is.True);
          Assert.That(current.Server.IsBroken, Is.False);
          Assert.That(GameStateDump.ToJson(current.Session), Is.EqualTo(before), "Партия не вернулась к состоянию до команды.");
          Assert.That(current.Server.Log, Has.Count.EqualTo(log), "Упавшая команда попала в журнал.");
          Assert.That(current.Server.Version, Is.GreaterThan(version), "После отката клиенты должны получить новое состояние.");
        });
        rolledBack++;
      },
    };

    wrapped.Play();

    Assert.That(injected, Is.GreaterThan(10));
    Assert.That(rolledBack, Is.EqualTo(injected));
    Assert.That(unchanged, Is.Zero, "Сбой случился до изменений — откат не проверен.");
    Assert.That(wrapped.Session.Phase, Is.EqualTo(GamePhase.GameOver));
  }

  [Test]
  public void FaultedEvent_ReportsCommandAndSeat()
  {
    var game = new NetworkBotGame(2, GameMode.Money, 41, checkRejectedAreClean: false);
    var reported = new List<(int Seat, GameCommand Command)>();
    game.Server.Faulted += (seat, command, _) => reported.Add((seat, command));
    var seat = game.Server.SeatOf(game.Session.CurrentPlayer);
    game.Server.FaultInjector = _ => throw new InvalidOperationException("Сбой.");

    var result = game.Server.Execute(seat, new PassCommand());

    Assert.That(result.Fault, Is.True);
    Assert.That(reported, Is.EqualTo(new[] { (seat, (GameCommand)new PassCommand()) }));
  }

  [Test]
  public void ActingSeat_IsCurrentPlayer()
  {
    var game = new NetworkBotGame(3, GameMode.Money, 51);
    Assert.That(game.Server.ActingSeat, Is.EqualTo(game.Server.SeatOf(game.Session.CurrentPlayer)));
  }

  [Test]
  public void RejectedCommand_DoesNotChangeVersionOrLog()
  {
    var game = new NetworkBotGame(3, GameMode.Money, 61);
    var other = (game.Server.ActingSeat!.Value + 1) % 3;

    var result = game.Server.Execute(other, new PassCommand());

    Assert.That(result.Accepted, Is.False);
    Assert.That(result.Fault, Is.False);
    Assert.That(game.Server.Version, Is.Zero);
    Assert.That(game.Server.Log, Is.Empty);
  }
}
