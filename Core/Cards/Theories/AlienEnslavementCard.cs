using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Инопланетяне порабощают людей»: убрать любого персонажа из личного штаба и получить
/// его стоимость и влияние.
/// </summary>
public sealed class AlienEnslavementCard : TheoryCard
{
  public AlienEnslavementCard()
    : base("Инопланетяне порабощают людей", TheoryCatalog.AlienEnslavement, new RemoveCharacterEffect())
  {
  }
}
