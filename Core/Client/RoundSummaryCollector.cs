using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Views;

namespace ConspiracyLeague.Core.Client;

/// <summary>
/// Сетевая игра: итоги конца раунда для окна сводки. За одним компьютером их отдаёт
/// контроллер конца раунда, а в сетевой игре конец раунда проводит сервер — итоги собираются из его событий:
/// закрытые и открытые штабы, доход каждой теории по игрокам. Когда начинается следующий раунд, сводка готова
/// (<see cref="Ready"/>) — как и за одним компьютером. После последнего раунда сводки нет: там показываются итоги партии.
/// </summary>
public sealed class RoundSummaryCollector(GameSession session)
{
  private readonly List<TheoryIncomePaid> _incomes = [];
  private readonly List<string> _closed = [];
  private readonly List<string> _reopened = [];
  private bool _collecting;

  /// <summary>
  /// Начался следующий раунд — показать итоги конца прошлого: доход по теориям и изменения штабов.
  /// </summary>
  public event Action<IReadOnlyList<TheoryIncome>, SolvencyChanges>? Ready;

  /// <summary>
  /// События обновления от сервера. Партия к этому моменту уже обновлена.
  /// </summary>
  public void Add(ServerUpdate update)
  {
    foreach (var tableEvent in update.Events)
    {
      switch (tableEvent)
      {
        // Все спасовали — начинается конец раунда.
        case PhaseChanged { Phase: GamePhase.HeadquartersCheck }:
          _collecting = true;
          _incomes.Clear();
          _closed.Clear();
          _reopened.Clear();
          break;
        case TheoryClosed closed when _collecting:
          _closed.Add(closed.Theory);
          break;
        case TheoryReopened reopened when _collecting:
          _reopened.Add(reopened.Theory);
          break;
        case TheoryIncomePaid income when _collecting:
          _incomes.Add(income);
          break;
        case PhaseChanged { Phase: GamePhase.PlayerTurns } when _collecting:
          _collecting = false;
          Finish();
          break;
        case PhaseChanged { Phase: GamePhase.FinalLeadersBonus or GamePhase.GameOver }:
          _collecting = false;
          break;
        // Сервер откатил партию — собранное могло относиться к отменённому.
        case StateReset:
          _collecting = false;
          break;
      }
    }
  }

  private void Finish()
  {
    var incomes = _incomes
      .Select(income => Column(income.Theory) is { } column
        ? new TheoryIncome(column, [], income.BySeat
          .Where(share => share.Seat >= 0 && share.Seat < session.Players.Count)
          .Select((share, index) => new MeepleIncome(session.Players[share.Seat], index, share.Amount))
          .ToList())
        : null)
      .OfType<TheoryIncome>()
      .ToList();
    var solvency = new SolvencyChanges(
      _closed.Select(Column).OfType<TheoryColumn>().ToList(),
      _reopened.Select(Column).OfType<TheoryColumn>().ToList());
    if (incomes.Count > 0 || solvency.Closed.Count > 0 || solvency.Reopened.Count > 0)
      Ready?.Invoke(incomes, solvency);
  }

  private TheoryColumn? Column(string name) => session.Field.TheoryColumns.FirstOrDefault(column => column.Theory.Name == name);
}
