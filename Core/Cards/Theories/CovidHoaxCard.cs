using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Ковид создан правительством»: применяется, чтобы получить
/// <see cref="MoneyPerClosedHeadquarters"/>$ за каждый закрытый штаб теории, где владелец. Пассивно — то же
/// в конце каждого раунда (<see cref="CovidHoaxPassive"/>).
/// </summary>
public sealed class CovidHoaxCard : TheoryCard
{
  /// <summary>
  /// Сколько $ владелец получает за каждый свой закрытый штаб теории.
  /// </summary>
  public const int MoneyPerClosedHeadquarters = 5;

  public CovidHoaxCard()
    : base(
      "Ковид создан правительством",
      TheoryCatalog.CovidHoax,
      new GainMoneyPerClosedHeadquartersEffect(MoneyPerClosedHeadquarters),
      new CovidHoaxPassive())
  {
  }
}
