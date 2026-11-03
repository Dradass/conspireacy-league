using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Тайное правительство»: выложить персонажа с руки в теорию взакрытом виде со скидкой 3$.
/// </summary>
public sealed class SecretGovernmentCard : TheoryCard
{
  /// <summary>
  /// Скидка на выкладывание, $.
  /// </summary>
  public const int Discount = 3;

  public SecretGovernmentCard()
    : base(
      "Тайное правительство",
      TheoryCatalog.SecretGovernment,
      new PlaceHiddenCardEffect(bonusPerHiddenCard: 0, joiningDiscount: Discount))
  {
  }
}
