using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Ленин — гриб»: применяется, чтобы сыграть своего персонажа из личного штаба со 100% успехом.
/// Пассивно — за каждый неудачный розыгрыш своего персонажа владелец получает деньги (<see cref="LeninMushroomPassive"/>).
/// </summary>
public sealed class LeninMushroomCard : TheoryCard
{
  public LeninMushroomCard()
    : base("Ленин — гриб", TheoryCatalog.LeninMushroom, new PlayOwnCardEffect(quick: false), new LeninMushroomPassive())
  {
  }
}
