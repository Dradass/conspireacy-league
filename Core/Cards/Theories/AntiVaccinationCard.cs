using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Антипрививочники»: получить 3$ за каждую свою теорию, где владелец. Пассивно — все штабы
/// теорий владельца сложнее закрыть на 1 (<see cref="AntiVaccinationPassive"/>).
/// </summary>
public sealed class AntiVaccinationCard : TheoryCard
{
  /// <summary>
  /// Сколько $ за каждую свою теорию.
  /// </summary>
  public const int PerTheory = 3;

  public AntiVaccinationCard()
    : base("Антипрививочники", TheoryCatalog.AntiVaccination, new GainMoneyPerOwnedTheoryEffect(PerTheory), new AntiVaccinationPassive())
  {
  }
}
