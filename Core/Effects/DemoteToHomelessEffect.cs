using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Понизить персонажа (<see cref="GameContext.TargetCell"/>) до бездомного. Денег действие не приносит.
/// </summary>
public sealed class DemoteToHomelessEffect : Effect
{
  public override string Description => "Понизить любого персонажа в бездомного";

  public override bool RequiresTargetCell => true;

  protected internal override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCellsCore(GameContext context) =>
    context.Field.DemoteTargets(context.CurrentPlayer);

  protected internal override PlacementCheck CheckTargetCellCore(Cell cell, GameContext context) =>
    context.Field.CheckDemote(cell, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбран персонаж.");
    context.Field.Demote(cell, context.CurrentPlayer);
  }
}
