using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Несколько действий подряд: «+1$ и приводит 1 адепта».
/// </summary>
public sealed class AllOfEffect : Effect
{
  /// <summary>
  /// Действия в порядке выполнения.
  /// </summary>
  public IReadOnlyList<Effect> Effects { get; }

  public AllOfEffect(params Effect[] effects)
  {
    if (effects.Length < 2)
      throw new ArgumentException("Нужно хотя бы два действия.", nameof(effects));
    Effects = effects;
  }

  public override string Description => string.Join(" и ", Effects.Select(e => e.Description));

  public override bool RequiresTheory => Effects.Any(e => e.RequiresTheory);

  public override bool CanTargetTheory(TheoryColumn column, Player player) =>
    Effects.Where(e => e.RequiresTheory).All(e => e.CanTargetTheory(column, player));

  public override bool CanTargetAdeptCard(Cell cell, PlayingField field) =>
    Effects.Where(e => e.RequiresTheory).All(e => e.CanTargetAdeptCard(cell, field));

  public override bool RequiresHandCard => Effects.Any(e => e.RequiresHandCard);

  public override bool RequiresTargetPlayer => Effects.Any(e => e.RequiresTargetPlayer);

  public override bool RequiresTargetCell => Effects.Any(e => e.RequiresTargetCell);

  public override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCells(GameContext context) =>
    TargetCellEffect?.TargetCells(context) ?? [];

  public override PlacementCheck CheckTargetCell(Cell cell, GameContext context) =>
    TargetCellEffect?.CheckTargetCell(cell, context) ?? base.CheckTargetCell(cell, context);

  /// <summary>
  /// Действие, для которого выбирают карточку на поле.
  /// </summary>
  private Effect? TargetCellEffect => Effects.FirstOrDefault(e => e.RequiresTargetCell);

  public override void Execute(GameContext context)
  {
    foreach (var effect in Effects)
      effect.Execute(context);
  }
}
