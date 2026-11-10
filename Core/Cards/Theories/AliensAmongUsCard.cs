using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Инопланетяне среди нас»: добавить на рынок случайного персонажа не из колоды рынка и получить $, равные его влиянию.
/// </summary>
public sealed class AliensAmongUsCard : TheoryCard
{
  public AliensAmongUsCard()
    : base("Инопланетяне среди нас", TheoryCatalog.AliensAmongUs, new AddReserveCardToMarketEffect())
  {
  }
}
