using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Заговор коммунистов»: превратить любого бездомного в гражданина.
/// </summary>
public sealed class CommunistConspiracyCard : TheoryCard
{
  public CommunistConspiracyCard()
    : base("Заговор коммунистов", TheoryCatalog.CommunistConspiracy, new TransformHomelessEffect())
  {
  }
}
