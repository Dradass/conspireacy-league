using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// При выкладывании в штаб теории (<see cref="GameContext.TargetTheory"/>): поменять местами две карты этой теории
/// (<see cref="GameContext.TargetCell"/> и <see cref="GameContext.SecondTargetCell"/>). Карты переезжают вместе
/// с отметками владельцев.
/// </summary>
public sealed class SwapCardsEffect : Effect
{
  public override string Description => "Поменять местами две карты в теории";

  public override bool RequiresTargetCell => true;

  public override bool RequiresSecondTargetCell => true;

  public override bool TargetIsOptional => true;

  protected internal override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCellsCore(GameContext context) =>
    context.Field.SwapTargets(Theory(context), context.TargetCell);

  protected internal override PlacementCheck CheckTargetCellCore(Cell cell, GameContext context) =>
    context.Field.CheckSwap(cell, Theory(context), context.TargetCell);

  public override void Execute(GameContext context)
  {
    if (context is { TargetCell: { } first, SecondTargetCell: { } second })
      context.Field.Swap(first, second, Theory(context));
  }

  private static TheoryColumn Theory(GameContext context) =>
    context.TargetTheory ?? throw new InvalidOperationException("Не указан штаб теории.");
}
