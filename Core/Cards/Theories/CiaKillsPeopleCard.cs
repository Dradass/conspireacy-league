using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «ЦРУ убивает людей»: убрать персонажа с рынка и получить $, равные его влиянию.
/// </summary>
public sealed class CiaKillsPeopleCard : TheoryCard
{
  public CiaKillsPeopleCard()
    : base("ЦРУ убивает людей", TheoryCatalog.CiaKillsPeople, new RemoveMarketCardForInfluenceEffect())
  {
  }
}
