using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards.Theories;

/// <summary>
/// Карточка теории «Химические следы самолёта»: получить деньги за адептов во всех штабах теорий
/// и убрать одного адепта из выбранной теории.
/// </summary>
public sealed class ChemtrailsCard : TheoryCard
{
  public ChemtrailsCard()
    : base(
      "Химические следы самолета",
      TheoryCatalog.Chemtrails,
      new AllOfEffect(new GainMoneyPerAllAdeptsEffect(), new RemoveAdeptEffect(1)))
  {
  }
}
