using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Field;

/// <summary>
/// Награда владельцу действующей теории в итоговом подсчёте: по <see cref="PerLeader"/>$ за каждого лидера теории.
/// </summary>
/// <param name="Column">Теория.</param>
/// <param name="Owner">Владелец теории (<see cref="TheoryColumn.Owner"/>).</param>
/// <param name="Amount">Сумма в $.</param>
public sealed record LeadersBonus(TheoryColumn Column, Player Owner, int Amount)
{
  /// <summary>
  /// Сколько $ владелец получает за каждого лидера теории.
  /// </summary>
  public const int PerLeader = 2;

  /// <summary>
  /// Посчитать награду владельцу теории.
  /// </summary>
  public static LeadersBonus Of(TheoryColumn column) =>
    new(column, column.Owner, (column.Members.Count + column.LeaderMeeples.Count) * PerLeader);
}
