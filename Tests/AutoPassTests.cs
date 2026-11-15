using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Tests;

public sealed class AutoPassTests
{
  /// <summary>
  /// Автопас срабатывает, только если ходить действительно нечем: в руке пусто, в личном штабе всё сыграно,
  /// рынок пуст и нет карточки теории, которую ещё можно применить.
  /// </summary>
  [TestCase(2, GameMode.Money, 601)]
  [TestCase(3, GameMode.TheoryDevelopment, 602)]
  [TestCase(4, GameMode.Race, 603)]
  [TestCase(5, GameMode.TheoryDevelopment, 604)]
  public void AutoPass_OnlyWhenNothingToPlay(int players, GameMode mode, int seed)
  {
    GameSession? watched = null;
    var autoPasses = 0;
    void OnAutoPassed(Player player)
    {
      autoPasses++;
      var session = watched!;
      Assert.That(player.Hand.Cards, Is.Empty, "В руке остались карточки.");
      Assert.That(session.Field.HasUnusedCards(player), Is.False, "В личном штабе остались несыгранные карточки.");
      Assert.That(session.Field.TheoryCards.Where(card => session.Field.CanUseTheoryCard(card, player)), Is.Empty,
        "Осталась карточка теории, которую можно применить.");
    }

    void Watch(NetworkBotGame current)
    {
      if (current.Session == watched)
        return;
      if (watched is not null)
        watched.AutoPassed -= OnAutoPassed;
      watched = current.Session;
      watched.AutoPassed += OnAutoPassed;
    }

    // Сервер может заменить партию при откате — следим за текущей.
    var game = new NetworkBotGame(players, mode, seed) { BeforeStep = Watch };
    Watch(game);
    game.Play();
    TestContext.Out.WriteLine($"Автопасов: {autoPasses}");
  }

  /// <summary>
  /// Рука пуста, личный штаб сыгран, рынок пуст, но есть карточка теории, которую можно применить, — ходить есть чем.
  /// </summary>
  [TestCase(3, GameMode.TheoryDevelopment, 611)]
  [TestCase(4, GameMode.Money, 612)]
  public void UsableTheoryCard_IsAMove(int players, GameMode mode, int seed)
  {
    var checkedOnce = false;
    var game = new NetworkBotGame(players, mode, seed)
    {
      BeforeStep = current =>
      {
        var session = current.Session;
        var player = session.Players.FirstOrDefault(player => !session.Field.HasUnusedCards(player)
          && session.Field.TheoryCards.Any(card => session.Field.CanUseTheoryCard(card, player)));
        if (player is null)
          return;

        player.Hand.Restore([]);
        foreach (var cell in session.Market.OpenCards.Keys.ToList())
          session.Market.RemoveOpenCard(cell);
        Assert.That(session.HasMoves(player), Is.True, "Карточку теории ещё можно применить — автопаса быть не должно.");
        checkedOnce = true;
        throw new StopGame();
      },
    };

    Assert.Throws<StopGame>(() => game.Play());
    Assert.That(checkedOnce, Is.True);
  }

  private sealed class StopGame : Exception;
}
