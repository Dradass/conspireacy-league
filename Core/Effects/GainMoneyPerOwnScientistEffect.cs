using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Игрок получает <see cref="PerScientist"/>$ за каждого своего ученого на поле.
/// </summary>
public sealed class GainMoneyPerOwnScientistEffect : Effect
{
  /// <summary>
  /// Сколько $ за каждого ученого.
  /// </summary>
  public int PerScientist { get; }

  public GainMoneyPerOwnScientistEffect(int perScientist)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(perScientist);
    PerScientist = perScientist;
  }

  public override string Description => $"Получить {PerScientist}$ за каждого своего ученого";

  public override void Execute(GameContext context) =>
    context.CurrentPlayer.Earn(context.Field.ScientistsOf(context.CurrentPlayer) * PerScientist);
}
