using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «5G вреди здоровью»: адепт из ближайшей теории слева и из ближайшей справа переходит в эту теорию.
/// </summary>
public sealed class FiveGTowersCard : TheoryCard
{
  public FiveGTowersCard()
    : base("5G вреди здоровью", TheoryCatalog.FiveGTowers, new MoveNeighbourAdeptsEffect())
  {
  }
}
