using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Двойники президентов»: заменить любого профессионала в личном штабе или в руке на первого попавшегося
/// политика из колоды. Пассивно — политики приносят больше (<see cref="PresidentsPassive"/>).
/// </summary>
public sealed class PresidentDoublesCard : TheoryCard
{
  public PresidentDoublesCard()
    : base(
      "Двойники президентов",
      TheoryCatalog.PresidentDoubles,
      new ReplaceProfessionalWithPoliticianEffect(),
      new PresidentsPassive())
  {
  }
}
