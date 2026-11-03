using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Убрать бездомного (<see cref="GameContext.TargetCell"/>) из личного штаба — любого игрока — или из теории
/// к себе на руку.
/// </summary>
public sealed class TakeHomelessToHandEffect : Effect
{
  public override string Description => "Убрать любого бездомного из личного штаба или теории к себе на руку";

  public override bool RequiresTargetCell => true;

  public override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCells(GameContext context) =>
    context.Field.HomelessToHandTargets(context.CurrentPlayer);

  public override PlacementCheck CheckTargetCell(Cell cell, GameContext context) =>
    context.Field.CheckTakeHomeless(cell, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбран бездомный.");
    context.Field.TakeHomeless(cell, context.CurrentPlayer);
  }
}
