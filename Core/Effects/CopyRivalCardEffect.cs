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

  /// <summary>
  /// Само копирование можно выполнить через чужое действие (учёный играет копирование актёра).
  /// </summary>
  public override bool CanBeBorrowed => true;

  protected internal override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCellsCore(GameContext context) =>
    context.Field.CopyTargets(context.CurrentPlayer).Where(target => target.Cell != context.SourceCell);

  protected internal override PlacementCheck CheckTargetCellCore(Cell cell, GameContext context) =>
    cell == context.SourceCell
      ? PlacementCheck.Deny("Нельзя скопировать действие самой этой карточки.")
      : context.Field.CheckCopy(cell, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    // Копирование через чужое действие выбирает карточку отдельно (CopiedCell), прямое — как BorrowedCell.
    var cell = context.CopiedCell ?? context.BorrowedCell ?? throw new InvalidOperationException("Не выбрана карточка.");
    context.Field.CopyRival(cell, context);
  }
}
