using ConspiracyLeague.Core.Client;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Tests;

public sealed class RoundSummaryCollectorTests
{
  /// <summary>
  /// Итоги конца раунда, собранные клиентом из событий сервера, совпадают с тем, что насчитал сервер: доход каждой
  /// теории по игрокам, закрытые и открытые штабы. Сводка появляется к началу каждого следующего раунда, после последнего — нет.
  /// </summary>
  [TestCase(GameMode.Money, 3, 801)]
  [TestCase(GameMode.TheoryDevelopment, 4, 802)]
  [TestCase(GameMode.Race, 5, 803)]
  public void MatchesServerRoundEnd(GameMode mode, int players, int seed)
  {
    var game = ClientMirrorTests.NewGame(players, mode, seed);
    game.DrainEvents();
    var mirror = new ClientMirror(game.ViewFor(0));
    var collector = new RoundSummaryCollector(mirror.Session);
    var summaries = new List<(IReadOnlyList<TheoryIncome> Incomes, SolvencyChanges Solvency)>();
    collector.Ready += (incomes, solvency) => summaries.Add((incomes, solvency));
    var expected = new List<(int Round, List<(string Theory, string Shares)> Incomes, List<string> Closed, List<string> Reopened)>();
    var round = game.Session.Round;

    while (game.RunBotStep())
    {
      var update = game.UpdateFor(0, game.DrainEvents());
      mirror.Apply(update.View);
      collector.Add(update);

      var session = game.Session;
      if (session.Round == round || session.Phase != GamePhase.PlayerTurns)
        continue;
      round = session.Round;
      var incomes = session.IncomeOfRound.Select(income => (income.Column.Theory.Name, Shares(income))).ToList();
      var closed = session.SolvencyOfRound.Closed.Select(column => column.Theory.Name).ToList();
      var reopened = session.SolvencyOfRound.Reopened.Select(column => column.Theory.Name).ToList();
      if (incomes.Count > 0 || closed.Count > 0 || reopened.Count > 0)
        expected.Add((round, incomes, closed, reopened));
    }

    Assert.That(game.Session.Phase, Is.EqualTo(GamePhase.GameOver));
    Assert.That(summaries, Has.Count.EqualTo(expected.Count), "Сводок не столько, сколько раундов с итогами.");
    for (var i = 0; i < expected.Count; i++)
    {
      var (incomes, solvency) = summaries[i];
      Assert.That(incomes.Select(income => (income.Column.Theory.Name, Shares(income))), Is.EqualTo(expected[i].Incomes), $"Раунд {expected[i].Round}: доход.");
      Assert.That(solvency.Closed.Select(column => column.Theory.Name), Is.EqualTo(expected[i].Closed), $"Раунд {expected[i].Round}: закрытые.");
      Assert.That(solvency.Reopened.Select(column => column.Theory.Name), Is.EqualTo(expected[i].Reopened), $"Раунд {expected[i].Round}: открытые.");
    }

    TestContext.WriteLine($"Сводок: {summaries.Count}; доходов в них: {summaries.Sum(summary => summary.Incomes.Count)}.");
  }

  private static string Shares(TheoryIncome income) =>
    string.Join(", ", income.ByPlayer.Where(entry => entry.Amount > 0).OrderBy(entry => entry.Player.Name).Select(entry => $"{entry.Player.Name} {entry.Amount}"))
    + $" = {income.Total}";
}
