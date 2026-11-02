using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// При выкладывании в штаб теории (<see cref="GameContext.TargetTheory"/>): можно убрать из игры одного бездомного
/// из этой теории (<see cref="GameContext.TargetCell"/>). Карты над ним опускаются.
/// </summary>
public sealed class RemoveHomelessEffect : Effect
{
  public override string Description => "Можно убрать бездомного из теории из игры";

  public override bool RequiresTargetCell => true;

  public override bool TargetIsOptional => true;

  public override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCells(GameContext context) =>
    context.Field.RemoveHomelessTargets(Theory(context), context.SourceCell);

  public override PlacementCheck CheckTargetCell(Cell cell, GameContext context) =>
    context.Field.CheckRemoveHomeless(cell, Theory(context), context.SourceCell);

  public override void Execute(GameContext context)
  {
    if (context.TargetCell is { } cell)
      context.Field.RemoveHomeless(cell, Theory(context), context.SourceCell);
  }

  private static TheoryColumn Theory(GameContext context) =>
    context.TargetTheory ?? throw new InvalidOperationException("Не указан штаб теории.");
}
