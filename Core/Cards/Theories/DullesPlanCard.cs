using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «План Даллеса»: понизить любого персонажа в бездомного, заплатив владельцу
/// пониженного персонажа его влияние (денег не приносит).
/// </summary>
public sealed class DullesPlanCard : TheoryCard
{
  public DullesPlanCard()
    : base("План Даллеса", TheoryCatalog.DullesPlan, new DemoteToHomelessEffect())
  {
  }
}
