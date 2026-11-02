using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Забрать своего персонажа из личного штаба (<see cref="GameContext.TargetCell"/>) в руку.
/// Карточку, которая играет это действие (и карточку, через которую его играют), забрать нельзя.
/// </summary>
public sealed class TakeToHandEffect : Effect
{
  public override string Description => "Забрать своего персонажа из личного штаба в руку";

  public override bool RequiresTargetCell => true;

  public override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCells(GameContext context) =>
    context.Field.TakeTargets(context.CurrentPlayer, Acting(context));

  public override PlacementCheck CheckTargetCell(Cell cell, GameContext context) =>
    context.Field.CheckTake(cell, context.CurrentPlayer, Acting(context));

  public override void Execute(GameContext context)
  {
    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбран персонаж.");
    context.Field.TakeToHand(cell, context.CurrentPlayer, Acting(context));
  }

  /// <summary>
  /// Карточки, которые выполняют действие: та, что его играет, и та, чьё действие она использует.
  /// </summary>
  private static Cell[] Acting(GameContext context) =>
    new[] { context.SourceCell, context.BorrowedCell }.OfType<Cell>().ToArray();
}
