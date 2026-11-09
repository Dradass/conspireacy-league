using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Раскрыть закрытую карточку другого игрока (<see cref="GameContext.TargetCell"/>): её настоящую лицевую сторону видят все.
/// </summary>
public sealed class RevealHiddenCardEffect : Effect
{
  public override string Description => $"Быстрое: раскрыть закрытого персонажа другого игрока, заплатив {PlayingField.RevealCost}$ его владельцу";

  public override bool IsQuick => true;

  public override bool RequiresTargetCell => true;

  protected internal override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCellsCore(GameContext context) =>
    context.Field.RevealTargets(context.CurrentPlayer);

  protected internal override PlacementCheck CheckTargetCellCore(Cell cell, GameContext context) =>
    context.Field.CheckReveal(cell, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбрана закрытая карточка.");
    context.Field.Reveal(cell, context.CurrentPlayer);
  }
}
