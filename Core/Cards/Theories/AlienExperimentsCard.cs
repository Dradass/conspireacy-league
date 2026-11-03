using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Инопланетяне проводят опыты»: забрать чужого персонажа из личного штаба, заплатив владельцу
/// его стоимость и влияние.
/// </summary>
public sealed class AlienExperimentsCard : TheoryCard
{
  public AlienExperimentsCard()
    : base("Инопланетяне проводят опыты", TheoryCatalog.AlienExperiments, new TakeRivalCharacterEffect())
  {
  }
}
