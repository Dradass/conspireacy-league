using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Быстрое действие: использовать чужого непрофессионала в личном штабе другого игрока (<see cref="GameContext.BorrowedCell"/>) —
/// выполнить для себя выбранный вариант его действия (<see cref="GameContext.BorrowedOption"/>).
/// Чужая карточка после этого переворачивается, как сыгранная.
/// </summary>
public sealed class UseRivalCardEffect : Effect
{
  public override string Description => "Быстрое: использовать чужого непрофессионала в личном штабе";

  public override bool IsQuick => true;

  public override bool RequiresTargetCell => true;

  public override bool BorrowsTargetCard => true;

  protected internal override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCellsCore(GameContext context) =>
    context.Field.BorrowTargets(context.CurrentPlayer);

  protected internal override PlacementCheck CheckTargetCellCore(Cell cell, GameContext context) =>
    context.Field.CheckBorrow(cell, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    var cell = context.BorrowedCell ?? throw new InvalidOperationException("Не выбран чужой персонаж.");
    context.Field.UseRival(cell, context);
  }
}
