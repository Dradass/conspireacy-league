using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Понизить персонажа (<see cref="GameContext.TargetCell"/>) до бездомного; игрок получает <see cref="Reward"/>$.
/// </summary>
public sealed class DemoteToHomelessEffect : Effect
{
  /// <summary>
  /// Сколько $ получает игрок.
  /// </summary>
  public const int Reward = 3;

  public override string Description => $"Понизить любого персонажа в бездомного и получить {Reward}$";

  public override bool RequiresTargetCell => true;

  public override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCells(GameContext context) =>
    context.Field.DemoteTargets(context.CurrentPlayer);

  public override PlacementCheck CheckTargetCell(Cell cell, GameContext context) =>
    context.Field.CheckDemote(cell, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбран персонаж.");
    context.Field.Demote(cell, context.CurrentPlayer);
    context.CurrentPlayer.Earn(Reward);
  }
}
