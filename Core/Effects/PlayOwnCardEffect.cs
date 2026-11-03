using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Сыграть другого своего персонажа из личного штаба (<see cref="GameContext.BorrowedCell"/>)
/// со 100% успехом — выполнить выбранный вариант его действия (<see cref="GameContext.BorrowedOption"/>) без проверки
/// успеха. Персонаж после этого переворачивается, как сыгранный.
/// </summary>
/// <remarks>
/// По умолчанию — быстрое действие (не тратит действие хода). Карточка теории «Ленин — гриб» — обычное действие.
/// </remarks>
public sealed class PlayOwnCardEffect : Effect
{
  private readonly bool _quick;

  /// <param name="quick">Быстрое действие: не тратит действие хода.</param>
  public PlayOwnCardEffect(bool quick = true) => _quick = quick;

  public override string Description =>
    _quick ? "Быстрое: сыграть своего персонажа со 100% успехом" : "Сыграть своего персонажа со 100% успехом";

  public override bool IsQuick => _quick;

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
