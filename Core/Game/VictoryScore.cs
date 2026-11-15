using ConspiracyLeague.Core.Field;

namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Победные очки игрока в режиме развития теорий.
/// </summary>
/// <param name="Player">Игрок.</param>
/// <param name="LeaderPoints">Сумма влияния всех лидеров в штабах теорий, где игрок владелец, × 2.</param>
/// <param name="AdeptPoints">Очки от адептов: каждый лидер теории получает свою долю, как при делении денег.</param>
/// <param name="TheoryCount">Сколько разных действующих теорий, где игрок владелец и у него есть карта эффекта этой теории.</param>
/// <param name="TheoryPoints">Очки за количество теорий (<see cref="VictoryPoints.TheoryPointsFor"/>).</param>
/// <param name="MoneyPoints">Очки за деньги на руках: 1 очко за каждые <see cref="VictoryPoints.MoneyPerPoint"/>$.</param>
public sealed record VictoryScore(
  Player Player,
  int LeaderPoints,
  int AdeptPoints,
  int TheoryCount,
  int TheoryPoints,
  int MoneyPoints)
{
  /// <summary>
  /// Все победные очки игрока.
  /// </summary>
  public int Total => LeaderPoints + AdeptPoints + TheoryPoints + MoneyPoints;
}

/// <summary>
/// Подсчёт победных очков в режиме развития теорий.
/// </summary>
public static class VictoryPoints
{
  /// <summary>
  /// Сколько очков даёт каждый единица влияния лидера (× 2).
  /// </summary>
  public const int LeaderPointsPerInfluence = 2;

  /// <summary>
  /// Сколько $ на руках нужно, чтобы получить одно очко.
  /// </summary>
  public const int MoneyPerPoint = 5;

  /// <summary>
  /// Очки за 0, 1, 2, 3, 4, 5, 6 теорий; дальше — <see cref="ExtraTheoryPoints"/> за каждую.
  /// </summary>
  private static readonly int[] TheoryTable = [0, 1, 5, 12, 25, 40, 70];

  private const int ExtraTheoryPoints = 20;

  /// <summary>
  /// Очки за количество разных теорий.
  /// </summary>
  public static int TheoryPointsFor(int count) =>
    count < TheoryTable.Length
      ? TheoryTable[count]
      : TheoryTable[^1] + (count - (TheoryTable.Length - 1)) * ExtraTheoryPoints;

  /// <summary>
  /// Очки от адептов каждому игроку. Считаются так же, как доход от адептов: открытые теории с адептами
  /// и закрытые, где владелец «Иллюминатов» в роли лидера (только ему).
  /// </summary>
  public static IReadOnlyDictionary<Player, int> AdeptPoints(PlayingField field)
  {
    var points = new Dictionary<Player, int>();
    foreach (var column in field.TheoryColumns.Where(column =>
      column.Adepts > 0 && (!column.IsClosed || field.IlluminatiBeneficiary(column) is not null)))
    {
      var income = TheoryIncome.Of(column);
      income = field.IlluminatiBeneficiary(column) is { } beneficiary ? income.OnlyFor(beneficiary) : income;
      foreach (var (owner, amount) in income.ByPlayer)
        points[owner] = points.GetValueOrDefault(owner) + amount;
    }
    return points;
  }

  /// <summary>
  /// Победные очки игрока по состоянию поля и его денег.
  /// </summary>
  public static VictoryScore Of(Player player, PlayingField field)
  {
    var leaderPoints = field.TheoryColumns
      .Where(column => column.Owner == player)
      .Sum(column => column.Influence) * LeaderPointsPerInfluence;

    var theoryCount = field.TheoryColumns
      .Where(column => column.Owner == player && column.IsActive)
      .Select(column => column.Theory)
      .Count(theory => field.TheoryCards.Any(owned => owned.Owner == player && owned.Card.Theory == theory));

    return new VictoryScore(
      player,
      LeaderPoints: leaderPoints,
      AdeptPoints: AdeptPoints(field).GetValueOrDefault(player),
      TheoryCount: theoryCount,
      TheoryPoints: TheoryPointsFor(theoryCount),
      MoneyPoints: player.Money / MoneyPerPoint);
  }
}
