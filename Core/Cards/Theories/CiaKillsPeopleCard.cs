using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «ЦРУ убивает людей»: убрать 1 адепта из любой теории и получить 2$.
/// </summary>
public sealed class CiaKillsPeopleCard : TheoryCard
{
  public CiaKillsPeopleCard()
    : base("ЦРУ убивает людей", TheoryCatalog.CiaKillsPeople, new AllOfEffect(new RemoveAdeptEffect(1), new GainMoneyEffect(2)))
  {
  }
}
