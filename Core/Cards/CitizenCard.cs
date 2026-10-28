using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards;

/// <summary>
/// Гражданин.
/// </summary>
public sealed class CitizenCard : PersonCard
{
  /// <param name="theory">Теория, к которой относится карточка.</param>
  public CitizenCard(Theory? theory = null)
    : base(
      name: "Гражданин",
      successChancePercent: 80,
      cost: 4,
      influence: 2,
      onPlayFromHand: new ChooseOneEffect(
        new GainMoneyEffect(3),
        new AddAdeptsEffect(1, AdeptDestination.Theory)),
      onPlacedInHeadquarters: new AllOfEffect(
        new GainMoneyEffect(1),
        new AddAdeptsEffect(1, AdeptDestination.TheoryHeadquarters)),
      theory: theory)
  {
  }
}
