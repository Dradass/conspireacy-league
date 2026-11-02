using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// При выкладывании в штаб теории (<see cref="GameContext.TargetTheory"/>): если у выложенной карты
/// (<see cref="GameContext.SourceCell"/>) та же теория, что и у штаба, привести <see cref="Count"/> адептов.
/// </summary>
public sealed class AddAdeptsInOwnTheoryEffect : Effect
{
  /// <summary>
  /// Количество адептов.
  /// </summary>
  public int Count { get; }

  public AddAdeptsInOwnTheoryEffect(int count)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
    Count = count;
  }

  public override string Description => $"Если теория карты совпадает с теорией штаба — привести {Count} адептов";

  public override void Execute(GameContext context)
  {
    var theory = context.TargetTheory ?? throw new InvalidOperationException("Не указан штаб теории.");
    var cell = context.SourceCell ?? throw new InvalidOperationException("Не указана выложенная карта.");
    if (context.Field.CardAt(cell)?.Theory == theory.Theory && !theory.IsClosed)
      context.Field.AddAdepts(theory, Count);
  }
}
