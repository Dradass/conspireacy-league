using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Убрать из игры любого персонажа из личного штаба (<see cref="GameContext.TargetCell"/>).
/// Игрок получает его стоимость и влияние.
/// </summary>
public sealed class RemoveCharacterEffect : Effect
{
  public override string Description => "Убрать любого персонажа из личного штаба и получить его стоимость и влияние в $";

  public override bool RequiresTargetCell => true;

  public override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCells(GameContext context) =>
    context.Field.RemovableCharacterTargets(context.CurrentPlayer);

  public override PlacementCheck CheckTargetCell(Cell cell, GameContext context) =>
    context.Field.CheckRemoveCharacter(cell, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбран персонаж.");
    context.Field.RemoveCharacter(cell, context.CurrentPlayer);
  }
}
