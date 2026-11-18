using ConspiracyLeague.Core.Bots;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Tests;

/// <summary>
/// Конец игры без раскрытия всех закрытых персонажей: владельцы сами решают, кого раскрыть (<see cref="GamePhase.FinalReveal"/>).
/// </summary>
public sealed class FinalRevealTests
{
  [TestCase(GameMode.Money)]
  [TestCase(GameMode.TheoryDevelopment)]
  [TestCase(GameMode.Race)]
  public void BotGames_FinishWithOwnersChoosingReveals(GameMode mode)
  {
    var seats = Enumerable.Range(1, 4).Select(i => ($"Бот {i}", BotProfile.Ultra)).ToList();

    var entries = BotArena.Play(seats, mode, games: 4, revealHiddenAtEnd: false);

    Assert.That(entries.Sum(entry => entry.Games), Is.EqualTo(16));
  }
}
