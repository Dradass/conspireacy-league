using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Перевернуть карточку другого игрока в его личном штабе (<see cref="GameContext.TargetCell"/>) рубашкой вверх,
/// как будто она уже сыграна. Игрок платит владельцу карточки влияние переворачиваемого персонажа.
/// </summary>
public sealed class FlipRivalCardEffect : Effect
{
  public override string Description => "Быстрое: перевернуть карточку другого игрока, заплатив её влияние владельцу";

  public override bool IsQuick => true;

  public override bool RequiresTargetCell => true;

  protected internal override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCellsCore(GameContext context) =>
    context.Field.FlipRivalTargets(context.CurrentPlayer);

  protected internal override PlacementCheck CheckTargetCellCore(Cell cell, GameContext context) =>
    context.Field.CheckFlipRival(cell, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбрана карточка другого игрока.");
    context.Field.FlipRival(cell, context.CurrentPlayer);
  }
}
