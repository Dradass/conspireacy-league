using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Игрок получает по <see cref="PerTheory"/>$ за каждую свою теорию, где он владелец.
/// </summary>
public sealed class GainMoneyPerOwnedTheoryEffect : Effect
{
  /// <summary>
  /// Сколько $ за каждую теорию.
  /// </summary>
  public int PerTheory { get; }

  public GainMoneyPerOwnedTheoryEffect(int perTheory)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(perTheory);
    PerTheory = perTheory;
  }

  public override string Description => $"Получить {PerTheory}$ за каждую свою теорию, где ты владелец";

  public override void Execute(GameContext context) =>
    context.CurrentPlayer.Earn(context.Field.TheoryColumns.Count(column => column.Owner == context.CurrentPlayer) * PerTheory);
}
