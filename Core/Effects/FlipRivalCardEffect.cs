using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Перевернуть карточку другого игрока в его личном штабе (<see cref="GameContext.TargetCell"/>) рубашкой вверх,
/// как будто она уже сыграна. Игрок платит в банк влияние переворачиваемого персонажа.
/// </summary>
public sealed class FlipRivalCardEffect : Effect
{
  public override string Description => "Быстрое: перевернуть карточку другого игрока, заплатив её влияние";

  public override bool IsQuick => true;

  public override bool RequiresTargetCell => true;

  public override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCells(GameContext context) =>
    context.Field.FlipRivalTargets(context.CurrentPlayer);

  public override PlacementCheck CheckTargetCell(Cell cell, GameContext context) =>
    context.Field.CheckFlipRival(cell, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбрана карточка другого игрока.");
    context.Field.FlipRival(cell, context.CurrentPlayer);
  }
}
