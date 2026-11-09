using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Шапочка из фольги»: положить карточку на персонажа в личном штабе. Персонаж получает 100%
/// успеха и защиту от любых эффектов. Карточка сразу переворачивается, а шапочка лежит на персонаже до конца раунда
/// (<see cref="TinFoilPassive"/>).
/// </summary>
public sealed class TinFoilHatCard : TheoryCard
{
  public TinFoilHatCard()
    : base("Шапочка из фольги", TheoryCatalog.TinFoilHat, new AttachTinFoilEffect(), new TinFoilPassive())
  {
  }
}
