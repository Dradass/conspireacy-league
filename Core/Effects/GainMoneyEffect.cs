using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Игрок получает деньги.
/// </summary>
public sealed class GainMoneyEffect : Effect
{
  /// <summary>
  /// Сумма в $.
  /// </summary>
  public int Amount { get; }

  public GainMoneyEffect(int amount)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
    Amount = amount;
  }

  public override string Description => $"Получить {Amount}$";

  public override void Execute(GameContext context)
  {
    // TODO: добавить Amount к деньгам текущего игрока.
    throw new NotImplementedException();
  }
}
