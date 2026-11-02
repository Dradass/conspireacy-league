using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Быстрое действие: сыграть другого своего персонажа из личного штаба (<see cref="GameContext.BorrowedCell"/>)
/// со 100% успехом — выполнить выбранный вариант его действия (<see cref="GameContext.BorrowedOption"/>) без проверки
/// успеха. Персонаж после этого переворачивается, как сыгранный.
/// </summary>
public sealed class PlayOwnCardEffect : Effect
{
  public override string Description => "Быстрое: сыграть своего персонажа со 100% успехом";

  public override bool IsQuick => true;

  public override bool RequiresTargetCell => true;

  public override bool BorrowsTargetCard => true;

  public override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCells(GameContext context) =>
    context.Field.OwnPlayTargets(context.CurrentPlayer);

  public override PlacementCheck CheckTargetCell(Cell cell, GameContext context) =>
    context.Field.CheckOwnPlay(cell, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    var cell = context.BorrowedCell ?? throw new InvalidOperationException("Не выбран свой персонаж.");
    context.Field.PlayOwn(cell, context);
  }
}
