using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Иллюминаты»: выложить персонажа взакрытую бесплатно; владелец соседнего чужого лидера
/// платит 1$. Пассивно — когда в теорию с вашим закрытым персонажем приходит чужой лидер, вы получаете 5$
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
        joiningDiscount: PlaceHiddenCardEffect.FreeDiscount,
        neighbourPays: true),
      new IlluminatiPassive())
  {
  }
}
