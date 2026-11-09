using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Превратить бездомного в личном штабе (<see cref="GameContext.TargetCell"/>) или в руке (<see cref="GameContext.HandCard"/>)
/// в гражданина той же теории.
/// </summary>
public sealed class TransformHomelessEffect : Effect
{
  public override string Description => "Превратить любого бездомного в личном штабе или в руке в гражданина";

  public override bool RequiresTargetCell => true;

  public override bool AcceptsHandCardTarget => true;

  protected internal override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCellsCore(GameContext context) =>
    context.Field.HomelessTargets();

  protected internal override PlacementCheck CheckTargetCellCore(Cell cell, GameContext context) =>
    context.Field.CheckTransformHomeless(cell);

  public override PlacementCheck CheckHandCard(Card card, GameContext context) =>
    context.Field.CheckTransformHandHomeless(card, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    if (context.HandCard is { } handCard)
    {
      context.Field.TransformHandHomeless(handCard, context.CurrentPlayer);
      return;
    }

    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбран бездомный.");
    context.Field.TransformHomeless(cell);
  }
}
