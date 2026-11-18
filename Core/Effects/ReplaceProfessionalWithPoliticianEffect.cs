using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Заменить профессионала в личном штабе (<see cref="GameContext.TargetCell"/>) или в своей руке
/// (<see cref="GameContext.HandCard"/>) на первого попавшегося политика (не из колоды). Заменённый профессионал уходит из игры.
/// </summary>
public sealed class ReplaceProfessionalWithPoliticianEffect : Effect
{
  public override string Description => "Заменить любого профессионала в личном штабе или в руке на первого попавшегося политика";

  public override bool RequiresTargetCell => true;

  public override bool AcceptsHandCardTarget => true;

  protected internal override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCellsCore(GameContext context) =>
    context.Field.ProfessionalTargets(context.CurrentPlayer);

  protected internal override PlacementCheck CheckTargetCellCore(Cell cell, GameContext context) =>
    context.Field.CheckReplaceWithPolitician(cell, context.CurrentPlayer);

  public override PlacementCheck CheckHandCard(Card card, GameContext context) =>
    context.Field.CheckReplaceHandWithPolitician(card, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    if (context.HandCard is { } handCard)
    {
      context.Field.ReplaceHandWithPolitician(handCard, context.CurrentPlayer);
      return;
    }

    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбран профессионал.");
    context.Field.ReplaceWithPolitician(cell, context.CurrentPlayer);
  }
}
