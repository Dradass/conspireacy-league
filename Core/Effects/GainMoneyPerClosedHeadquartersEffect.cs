using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Игрок получает по <see cref="PerHeadquarters"/>$ за каждый закрытый штаб теории, где он владелец.
/// </summary>
public sealed class GainMoneyPerClosedHeadquartersEffect : Effect
{
  /// <summary>
  /// Сколько $ за каждый закрытый штаб.
  /// </summary>
  public int PerHeadquarters { get; }

  public GainMoneyPerClosedHeadquartersEffect(int perHeadquarters)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(perHeadquarters);
    PerHeadquarters = perHeadquarters;
  }

  public override string Description => $"Получить {PerHeadquarters}$ за каждый закрытый штаб теории, где ты владелец";

  public override void Execute(GameContext context) =>
    context.CurrentPlayer.Earn(context.Field.ClosedHeadquartersOf(context.CurrentPlayer).Count() * PerHeadquarters);
}
