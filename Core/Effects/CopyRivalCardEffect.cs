using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Скопировать действие открытой (несыгранной) карточки в личном штабе другого игрока (<see cref="GameContext.BorrowedCell"/>):
/// выполнить для себя выбранный вариант её действия (<see cref="GameContext.BorrowedOption"/>).
/// Чужая карточка при этом не переворачивается. Карточки, которые защищает полицейский, копировать нельзя.
/// </summary>
public sealed class CopyRivalCardEffect : Effect
{
  public override string Description => "Скопировать действие открытой карты другого игрока";

  public override bool RequiresTargetCell => true;

  public override bool BorrowsTargetCard => true;

  public override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCells(GameContext context) =>
    context.Field.CopyTargets(context.CurrentPlayer);

  public override PlacementCheck CheckTargetCell(Cell cell, GameContext context) =>
    context.Field.CheckCopy(cell, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    var cell = context.BorrowedCell ?? throw new InvalidOperationException("Не выбрана карточка другого игрока.");
    context.Field.CopyRival(cell, context);
  }
}
