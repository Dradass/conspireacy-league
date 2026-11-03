using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Инопланетяне среди нас»: превратить любого бездомного в гражданина.
/// </summary>
public sealed class AliensAmongUsCard : TheoryCard
{
  public AliensAmongUsCard()
    : base("Инопланетяне среди нас", TheoryCatalog.AliensAmongUs, new TransformHomelessEffect())
  {
  }
}
