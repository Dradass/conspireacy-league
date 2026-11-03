using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Плоская Земля»: добавить адептов в три любые теории. Пассивно — основатель теории имеет +3 влияния,
/// а её штаб нельзя закрыть (<see cref="FlatEarthPassive"/>).
/// </summary>
public sealed class FlatEarthCard : TheoryCard
{
  public FlatEarthCard()
    : base("Плоская Земля", TheoryCatalog.FlatEarth, new AddAdeptsToThreeTheoriesEffect(), new FlatEarthPassive())
  {
  }
}
