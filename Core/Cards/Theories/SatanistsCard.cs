using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Сатанисты похищают детей»: убрать любого бездомного из личного штаба к себе на руку.
/// Из штабов теорий забирать нельзя.
/// Пассивно — бездомные приносят больше (<see cref="SatanistsPassive"/>).
/// </summary>
public sealed class SatanistsCard : TheoryCard
{
  public SatanistsCard()
    : base(
      "Сатанисты похищают детей",
      TheoryCatalog.SatanistsKidnapChildren,
      new TakeHomelessToHandEffect(),
      new SatanistsPassive())
  {
  }
}
