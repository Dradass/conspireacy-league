using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Заговор науки»: добавить адепта в теорию со своим лидером и гражданином, или двух — если там профессионал.
/// </summary>
public sealed class ScienceConspiracyCard : TheoryCard
{
  public ScienceConspiracyCard()
    : base("Заговор науки", TheoryCatalog.ScienceConspiracy, new AddAdeptsToOwnLeaderTheoryEffect())
  {
  }
}
