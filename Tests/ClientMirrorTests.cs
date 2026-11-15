using ConspiracyLeague.Core.Bots;
using ConspiracyLeague.Core.Client;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Server;
using ConspiracyLeague.Core.Views;

namespace ConspiracyLeague.Core.Tests;

public sealed class ClientMirrorTests
{
  internal static ServerGame NewGame(int players, GameMode mode, int seed, int humans = 0)
  {
    var levels = Enum.GetValues<BotLevel>();
    return new ServerGame(new GameSetup(
      Enum.GetValues<PlayerColor>().Take(players)
        .Select((color, index) => new SeatSetup($"Игрок {index + 1}", color, IsBot: index >= humans, levels[(seed + index) % levels.Length]))
        .ToList(),
      mode,
      FailureConsequences: seed % 2 == 0,
      NetworkBotGame.KeyFrom(seed)));
  }

  /// <summary>
  /// Вид партии → зеркало → вид: зеркало, собранное по виду, даёт тот же вид — и заново, и обновлением прежнего зеркала.
  /// Значит, клиент восстанавливает всё, что видит игрок, и правила по зеркалу считают то же, что на сервере.
  /// </summary>
  [TestCase(GameMode.Money, 2, 201)]
  [TestCase(GameMode.Money, 4, 202)]
  [TestCase(GameMode.TheoryDevelopment, 3, 203)]
  [TestCase(GameMode.TheoryDevelopment, 5, 204)]
  [TestCase(GameMode.Race, 4, 205)]
  [TestCase(GameMode.Race, 6, 206)]
  public void MirrorReproducesView(GameMode mode, int players, int seed)
  {
    var game = NewGame(players, mode, seed);
    var seats = Enumerable.Range(0, players).Select(seat => (int?)seat).Append(null).ToList();
    var mirrors = seats.ToDictionary(seat => seat ?? -1, seat => new ClientMirror(game.ViewFor(seat)));
    var checks = 0;

    for (var step = 0; game.RunBotStep(); step++)
    {
      Assert.That(step, Is.LessThan(20_000));
      foreach (var seat in seats)
      {
        var view = game.ViewFor(seat);
        var mirror = mirrors[seat ?? -1];
        mirror.Apply(view);
        AssertSame(view, mirror, $"шаг {step}, место {seat?.ToString() ?? "зритель"}, обновление");
        if (step % 7 == 0)
          AssertSame(view, new ClientMirror(view), $"шаг {step}, место {seat?.ToString() ?? "зритель"}, заново");
        checks++;
      }
    }

    Assert.That(game.Session.Phase, Is.EqualTo(GamePhase.GameOver));
    TestContext.WriteLine($"Проверок: {checks}.");
  }

  /// <summary>
  /// Выбранная в интерфейсе карточка переживает обновление: тот же идентификатор и та же сторона — тот же объект.
  /// </summary>
  [Test]
  public void Apply_KeepsCardObjects()
  {
    var game = NewGame(3, GameMode.Money, 210, humans: 3);
    var mirror = new ClientMirror(game.ViewFor(0));
    var hand = mirror.Session.Players[0].Hand.Cards.ToList();
    var restored = 0;
    mirror.Session.Restored += () => restored++;

    mirror.Apply(game.ViewFor(0));

    Assert.That(mirror.Session.Players[0].Hand.Cards, Is.EqualTo(hand).Using<Cards.Card>(ReferenceEquals));
    Assert.That(restored, Is.EqualTo(1));
  }

  [Test]
  public void Apply_RejectsViewForAnotherSeat()
  {
    var game = NewGame(3, GameMode.Money, 211, humans: 3);
    var mirror = new ClientMirror(game.ViewFor(0));
    Assert.Throws<InvalidDataException>(() => mirror.Apply(game.ViewFor(1)));
  }

  /// <summary>
  /// Противоречивый вид не меняет зеркало.
  /// </summary>
  [Test]
  public void Apply_BrokenViewLeavesMirrorUnchanged()
  {
    var game = NewGame(3, GameMode.Money, 212, humans: 3);
    var view = game.ViewFor(0);
    var mirror = new ClientMirror(view);
    var before = ViewSerializer.Serialize(PlayerViews.For(mirror.Session, 0, view.Version));
    var broken = view with { Hand = view.Hand.Select(card => card with { Id = view.Hand[0].Id }).ToList() };

    Assert.Throws<InvalidDataException>(() => mirror.Apply(broken));
    Assert.That(ViewSerializer.Serialize(PlayerViews.For(mirror.Session, 0, view.Version)), Is.EqualTo(before));
  }

  /// <summary>
  /// Команду, собранную по зеркалу (идентификаторы — те, что выдал сервер), сервер принимает.
  /// </summary>
  [Test]
  public void CommandsFromMirror_AreAccepted()
  {
    var game = NewGame(2, GameMode.Money, 213, humans: 2);
    var seat = game.ActingSeat!.Value;
    var mirror = new ClientMirror(game.ViewFor(seat));
    var session = mirror.Session;
    var player = session.Players[seat];
    var (card, cell) = player.Hand.Cards
      .SelectMany(card => session.Field.AvailableCells(card, player).Where(target => target.Check.IsAllowed).Select(target => (card, target.Cell)))
      .First();

    var result = game.Execute(seat, new Commands.PlaceCommand(session.CardIds.IdOf(card), cell));

    Assert.That(result.Accepted, Is.True, result.Error);
  }

  private static int _targetMismatches;

  [OneTimeTearDown]
  public void ReportTargets() => TestContext.Progress.WriteLine($"Списки целей по зеркалу разошлись с сервером: {_targetMismatches} раз.");

  /// <summary>
  /// Подходящие цели зависят и от скрытого (лицевой стороны закрытой карты), поэтому их клиент берёт из вида
  /// (<see cref="ClientMirror.View"/>), а не считает по зеркалу. Здесь они не сравниваются, только считаются расхождения.
  /// </summary>
  private static PlayerView WithTargetsOf(PlayerView actual, PlayerView view)
  {
    var placement = actual.PendingPlacement is { } p && view.PendingPlacement is { } expected
      ? p with { AllowedTargets = expected.AllowedTargets }
      : actual.PendingPlacement;
    var trigger = actual.PendingTrigger is { } t && view.PendingTrigger is { } expectedTrigger
      ? t with { AllowedCells = expectedTrigger.AllowedCells, AllowedHandCards = expectedTrigger.AllowedHandCards, AllowedTheories = expectedTrigger.AllowedTheories }
      : actual.PendingTrigger;
    var normalized = actual with { PendingPlacement = placement, PendingTrigger = trigger };
    if (ViewSerializer.Serialize(normalized) != ViewSerializer.Serialize(actual))
      _targetMismatches++;
    return normalized;
  }

  private static void AssertSame(PlayerView view, ClientMirror mirror, string where)
  {
    var expected = ViewSerializer.Serialize(view);
    var actual = ViewSerializer.Serialize(WithTargetsOf(PlayerViews.For(mirror.Session, view.Viewer, view.Version), view));
    if (expected == actual)
      return;

    var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i]);
    Assert.Fail($"{where}: вид по зеркалу другой.\nОжидалось: …{Around(expected, index)}…\nПолучено:  …{Around(actual, index)}…");
  }

  private static string Around(string text, int index) => text[Math.Max(0, index - 200)..Math.Min(text.Length, index + 200)];
}
