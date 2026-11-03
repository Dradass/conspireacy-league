using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Скопировать действие открытой (несыгранной) карточки в личном штабе (своём или чужом, <see cref="GameContext.BorrowedCell"/>),
/// кроме самой копирующей карточки:
/// выполнить для себя выбранный вариант её действия (<see cref="GameContext.BorrowedOption"/>).
/// Чужая карточка при этом не переворачивается. Карточки, которые защищает полицейский, копировать нельзя.
/// </summary>
public sealed class CopyRivalCardEffect : Effect
{
  public override string Description => "Скопировать действие открытой карты в личном штабе";

  public override bool RequiresTargetCell => true;

  public override bool BorrowsTargetCard => true;

  public override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCells(GameContext context) =>
    context.Field.CopyTargets(context.CurrentPlayer).Where(target => target.Cell != context.SourceCell);

  public override PlacementCheck CheckTargetCell(Cell cell, GameContext context) =>
    cell == context.SourceCell
      ? PlacementCheck.Deny("Нельзя скопировать действие самой этой карточки.")
      : context.Field.CheckCopy(cell, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    var cell = context.BorrowedCell ?? throw new InvalidOperationException("Не выбрана карточка.");
    context.Field.CopyRival(cell, context);
  }
}
