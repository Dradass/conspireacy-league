using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Инопланетяне поставляют новые технологии»: получить 5$ за каждого своего ученого.
/// Пассивно — все ученые владельца дают больше (<see cref="ScientistBoostPassive"/>).
/// </summary>
public sealed class AlienTechnologyCard : TheoryCard
{
  /// <summary>
  /// Сколько $ за каждого своего ученого.
  /// </summary>
  public const int PerScientist = 5;

  public AlienTechnologyCard()
    : base(
      "Инопланетяне поставляют новые технологии",
      TheoryCatalog.AlienTechnology,
      new GainMoneyPerOwnScientistEffect(PerScientist),
      new ScientistBoostPassive())
  {
  }
}
