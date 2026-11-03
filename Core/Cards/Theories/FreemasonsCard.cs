using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Масоны правят миром»: выложить персонажа взакрытую и получить 1$ за каждого своего закрытого
/// персонажа на поле. Пассивно — все ваши закрытые персонажи имеют 3 влияния (<see cref="MasonsPassive"/>).
/// </summary>
public sealed class FreemasonsCard : TheoryCard
{
  /// <summary>
  /// Сколько $ за каждого своего закрытого персонажа на поле.
  /// </summary>
  public const int BonusPerHiddenCard = 1;

  public FreemasonsCard()
    : base(
      "Масоны правят миром",
      TheoryCatalog.Freemasons,
      new PlaceHiddenCardEffect(bonusPerHiddenCard: BonusPerHiddenCard),
      new MasonsPassive())
  {
  }
}
