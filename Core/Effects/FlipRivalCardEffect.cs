using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Перевернуть карточку другого игрока в его личном штабе (<see cref="GameContext.TargetCell"/>) рубашкой вверх,
/// как будто она уже сыграна. Игрок платит в банк влияние переворачиваемого персонажа.
/// </summary>
public sealed class FlipRivalCardEffect : Effect
{
  public override string Description => "Перевернуть карточку другого игрока, заплатив её влияние";

  public override bool RequiresRivalCard => true;

  public override void Execute(GameContext context)
  {
    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбрана карточка другого игрока.");
    context.Field.FlipRival(cell, context.CurrentPlayer);
  }
}
