using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Персонажу в своём личном штабе (<see cref="GameContext.TargetCell"/>) или в руке (<see cref="GameContext.HandCard"/>):
/// убрать теорию, если она есть, а если нет — добавить случайную.
/// </summary>
public sealed class ToggleTheoryEffect : Effect
{
  public override string Description =>
    "Убрать теорию у персонажа в руке или в личном штабе, а если теории нет — добавить случайную";

  public override bool RequiresTargetCell => true;

  public override bool AcceptsHandCardTarget => true;

  protected internal override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCellsCore(GameContext context) =>
    context.Field.ToggleTheoryTargets(context.CurrentPlayer, context.SourceCell);

  protected internal override PlacementCheck CheckTargetCellCore(Cell cell, GameContext context) =>
    context.Field.CheckToggleTheory(cell, context.CurrentPlayer, context.SourceCell);

  public override PlacementCheck CheckHandCard(Card card, GameContext context) =>
    context.Field.CheckToggleHandTheory(card, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    if (context.HandCard is { } handCard)
    {
      context.Field.ToggleHandTheory(handCard, context.CurrentPlayer, context.Random);
      return;
    }

    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбран персонаж.");
    context.Field.ToggleTheory(cell, context.CurrentPlayer, context.SourceCell, context.Random);
  }
}
