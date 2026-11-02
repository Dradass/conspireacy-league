using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Открыть на рынке дополнительные карточки из колоды (<see cref="Field.Market.OpenExtraCards"/>) и получить деньги.
/// </summary>
public sealed class OpenMarketCardsEffect : Effect
{
  /// <summary>
  /// Сколько карточек открыть.
  /// </summary>
  public int Count { get; }

  /// <summary>
  /// Сколько $ получает игрок.
  /// </summary>
  public int Money { get; }

  public OpenMarketCardsEffect(int count, int money)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
    ArgumentOutOfRangeException.ThrowIfNegative(money);
    Count = count;
    Money = money;
  }

  public override string Description => $"Добавить на рынок {Count} карты из колоды и получить {Money}$";

  public override void Execute(GameContext context)
  {
    var market = context.Market ?? throw new InvalidOperationException("Для действия нужен рынок.");
    market.OpenExtraCards(Count);
    context.CurrentPlayer.Earn(Money);
  }
}
