namespace ConspiracyLeague.Core.Field;

/// <summary>
/// Можно ли положить карточку в ячейку, и на каких условиях.
/// </summary>
public sealed record PlacementCheck
{
  /// <summary>
  /// Можно ли положить карточку.
  /// </summary>
  public bool IsAllowed { get; private init; }

  /// <summary>
  /// Сколько $ придётся заплатить.
  /// </summary>
  public int Cost { get; private init; }

  /// <summary>
  /// Почему нельзя положить карточку.
  /// </summary>
  public string? Reason { get; private init; }

  /// <summary>
  /// Стопка теории, к которой присоединится карточка. Отсутствует, если карточка начинает новую теорию
  /// или кладётся в рабочую зону игрока.
  /// </summary>
  public TheoryColumn? Column { get; private init; }

  /// <summary>
  /// Карточка начинает новую родительскую теорию, и на поле появляется штаб теории.
  /// </summary>
  public bool CreatesHeadquarters { get; private init; }

  internal static PlacementCheck Allow(int cost = 0, TheoryColumn? column = null, bool createsHeadquarters = false) =>
    new() { IsAllowed = true, Cost = cost, Column = column, CreatesHeadquarters = createsHeadquarters };

  internal static PlacementCheck Deny(string reason) => new() { Reason = reason };
}
