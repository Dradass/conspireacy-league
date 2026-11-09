using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Убрать бездомного (<see cref="GameContext.TargetCell"/>) из личного штаба — любого игрока — к себе на руку.
/// Из штабов теорий бездомных не забирают.
/// </summary>
public sealed class TakeHomelessToHandEffect : Effect
{
  public override string Description => "Убрать любого бездомного из личного штаба к себе на руку";

  public override bool RequiresTargetCell => true;

  protected internal override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCellsCore(GameContext context) =>
    context.Field.HomelessToHandTargets(context.CurrentPlayer);

  protected internal override PlacementCheck CheckTargetCellCore(Cell cell, GameContext context) =>
    context.Field.CheckTakeHomeless(cell, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбран бездомный.");
    context.Field.TakeHomeless(cell, context.CurrentPlayer);
  }
}
