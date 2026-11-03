using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Положить фишку на любого персонажа в личных штабах (<see cref="GameContext.TargetCell"/>): игрок платит
/// владельцу стоимость персонажа.
/// </summary>
public sealed class ChipPersonEffect : Effect
{
  public override string Description => "Положить фишку на любого персонажа в личных штабах и заплатить владельцу его стоимость";

  public override bool RequiresTargetCell => true;

  public override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCells(GameContext context) =>
    context.Field.ChipTargets(context.CurrentPlayer);

  public override PlacementCheck CheckTargetCell(Cell cell, GameContext context) =>
    context.Field.CheckChip(cell, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбран персонаж.");
    context.Field.Chip(cell, context.CurrentPlayer);
  }
}
