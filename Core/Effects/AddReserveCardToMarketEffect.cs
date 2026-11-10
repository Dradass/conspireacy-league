using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Выложить на рынок случайного персонажа не из колоды рынка (из резерва состава игры)
/// и получить $, равные его влиянию.
/// </summary>
public sealed class AddReserveCardToMarketEffect : Effect
{
  public override string Description => "Добавить на рынок случайного персонажа и получить $, равные его влиянию";

  public override void Execute(GameContext context)
  {
    var market = context.Market ?? throw new InvalidOperationException("Нет рынка.");
    var card = market.AddReserveCard(context.Random);
    if (card is not null)
      context.CurrentPlayer.Earn(card.Influence);
  }
}
