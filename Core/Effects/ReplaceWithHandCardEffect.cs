using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Заменить бездомного или гражданина в любой теории (<see cref="GameContext.TargetCell"/>) на карточку с руки
/// (<see cref="GameContext.HandCard"/>). Карточка с руки должна быть той же теории, что и заменяемая,
/// либо обе без теории. Заменённая карточка уходит из игры.
/// </summary>
public sealed class ReplaceWithHandCardEffect : Effect
{
  public override string Description => "Заменить бездомного или гражданина в любой теории на карточку с руки";

  public override bool RequiresHandCard => true;

  public override bool RequiresTargetCell => true;

  protected internal override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCellsCore(GameContext context) =>
    context.Field.ReplaceTargets(context.HandCard as PersonCard, context.CurrentPlayer);

  protected internal override PlacementCheck CheckTargetCellCore(Cell cell, GameContext context) =>
    context.Field.CheckReplace(cell, context.HandCard as PersonCard, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбран персонаж для замены.");
    var card = context.HandCard as PersonCard ?? throw new InvalidOperationException("Не выбрана карточка в руке.");
    context.Field.Replace(cell, card, context.CurrentPlayer);
  }
}
