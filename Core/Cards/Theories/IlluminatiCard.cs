using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Иллюминаты»: выложить персонажа взакрытую бесплатно. Пассивно — вы получаете деньги за закрытые
/// теории, где вы лидер, а ваши закрытые карты при закрытии теории считаются бездомными
/// (<see cref="IlluminatiPassive"/>).
/// </summary>
public sealed class IlluminatiCard : TheoryCard
{
  public IlluminatiCard()
    : base(
      "Иллюминаты",
      TheoryCatalog.Illuminati,
      new PlaceHiddenCardEffect(
        bonusPerHiddenCard: 0,
        joiningDiscount: PlaceHiddenCardEffect.FreeDiscount),
      new IlluminatiPassive())
  {
  }
}
