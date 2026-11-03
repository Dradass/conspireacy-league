using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Убрать из игры <see cref="Count"/> адепта из выбранной теории (<see cref="GameContext.TargetTheory"/>).
/// </summary>
public sealed class RemoveAdeptEffect : Effect
{
  /// <summary>
  /// Сколько адептов убрать.
  /// </summary>
  public int Count { get; }

  public RemoveAdeptEffect(int count)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
    Count = count;
  }

  public override string Description => $"Убрать {Count} адепта из теории";

  public override bool RequiresTheory => true;

  public override bool CanTargetTheory(TheoryColumn column, Player player) =>
    base.CanTargetTheory(column, player) && column.Adepts >= Count && !column.AdeptsShielded;

  public override void Execute(GameContext context)
  {
    var theory = context.TargetTheory ?? throw new InvalidOperationException("Не выбрана теория.");
    context.Field.RemoveAdepts(theory, Count);
  }
}
