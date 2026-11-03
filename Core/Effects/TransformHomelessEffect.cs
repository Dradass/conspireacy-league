using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Превратить бездомного в личном штабе (<see cref="GameContext.TargetCell"/>) в гражданина той же теории.
/// </summary>
public sealed class TransformHomelessEffect : Effect
{
  public override string Description => "Превратить любого бездомного в личном штабе в гражданина";

  public override bool RequiresTargetCell => true;

  public override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCells(GameContext context) =>
    context.Field.HomelessTargets();

  public override PlacementCheck CheckTargetCell(Cell cell, GameContext context) =>
    context.Field.CheckTransformHomeless(cell);

  public override void Execute(GameContext context)
  {
    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбран бездомный.");
    context.Field.TransformHomeless(cell);
  }
}
