using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Коммунисты на Луне»: получить 1$ за каждого лидера во всех штабах теорий коммунистов.
/// Пассивно — при добавлении лидера выкладывается два мипла (<see cref="CommunistsOnMoonPassive"/>).
/// </summary>
public sealed class CommunistsOnMoonCard : TheoryCard
{
  public CommunistsOnMoonCard()
    : base("Коммунисты на Луне", TheoryCatalog.CommunistsOnMoon, new GainMoneyPerCommunistLeaderEffect(), new CommunistsOnMoonPassive())
  {
  }
}
