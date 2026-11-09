using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Быстрое действие: посмотреть в закрытую карточку другого игрока (<see cref="GameContext.TargetCell"/>).
/// Её видит только тот, кто посмотрел, и дальше он может смотреть её в любой момент, как владелец.
/// </summary>
public sealed class PeekHiddenCardEffect : Effect
{
  public override string Description => "Быстрое: посмотреть любую закрытую карту";

  public override bool IsQuick => true;

  public override bool RequiresTargetCell => true;

  protected internal override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCellsCore(GameContext context) =>
    context.Field.PeekTargets(context.CurrentPlayer);

  protected internal override PlacementCheck CheckTargetCellCore(Cell cell, GameContext context) =>
    context.Field.CheckPeek(cell, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбрана закрытая карточка.");
    context.Field.Peek(cell, context.CurrentPlayer);
  }
}
