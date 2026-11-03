using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Игрок получает по <see cref="PerLeader"/>$ за каждого лидера во всех закрытых теориях на поле.
/// </summary>
public sealed class GainMoneyPerLeaderInClosedTheoriesEffect : Effect
{
  /// <summary>
  /// Сколько $ за каждого лидера.
  /// </summary>
  public int PerLeader { get; }

  public GainMoneyPerLeaderInClosedTheoriesEffect(int perLeader)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(perLeader);
    PerLeader = perLeader;
  }

  public override string Description => $"Получить {PerLeader}$ за каждого лидера во всех закрытых теориях на поле";

  public override void Execute(GameContext context) =>
    context.CurrentPlayer.Earn(context.Field.TheoryColumns.Where(column => column.IsClosed).Sum(column => column.Leaders) * PerLeader);
}
