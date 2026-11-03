using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Заменить профессионала в личном штабе (<see cref="GameContext.TargetCell"/>) на первого попавшегося политика
/// (не из колоды). Заменённый профессионал уходит из игры.
/// </summary>
public sealed class ReplaceProfessionalWithPoliticianEffect : Effect
{
  public override string Description => "Заменить любого профессионала в личном штабе на первого попавшегося политика";

  public override bool RequiresTargetCell => true;

  public override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCells(GameContext context) =>
    context.Field.ProfessionalTargets(context.CurrentPlayer);

  public override PlacementCheck CheckTargetCell(Cell cell, GameContext context) =>
    context.Field.CheckReplaceWithPolitician(cell, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбран профессионал.");
    context.Field.ReplaceWithPolitician(cell, context.CurrentPlayer);
  }
}
