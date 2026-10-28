using ConspiracyLeague.Core.Effects;

namespace ConspiracyLeague.Core.Cards;

/// <summary>
/// Гражданин.
/// </summary>
public sealed class CitizenCard : PersonCard
{
  public CitizenCard()
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
        new AddAdeptsEffect(1, AdeptDestination.TheoryHeadquarters)))
  {
  }
}
