using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Забрать чужого персонажа из личного штаба (<see cref="GameContext.TargetCell"/>) в руку.
/// Игрок платит владельцу персонажа его стоимость и <see cref="PlayingField.RivalExtraPayment"/>$.
/// </summary>
public sealed class TakeRivalCharacterEffect : Effect
{
  public override string Description =>
    $"Забрать чужого персонажа из личного штаба в руку, заплатив его владельцу его стоимость и {PlayingField.RivalExtraPayment}$";

  public override bool RequiresTargetCell => true;

  protected internal override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCellsCore(GameContext context) =>
    context.Field.RivalCharacterTargets(context.CurrentPlayer);

  protected internal override PlacementCheck CheckTargetCellCore(Cell cell, GameContext context) =>
    context.Field.CheckTakeRival(cell, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбран персонаж.");
    context.Field.TakeRival(cell, context.CurrentPlayer);
  }
}
