using ConspiracyLeague.Core.Effects;

namespace ConspiracyLeague.Core.Cards;

/// <summary>
/// Бездомный.
/// </summary>
public sealed class HomelessCard : PersonCard
{
  public HomelessCard()
    : base(
      name: "Бездомный",
      successChancePercent: 50,
      cost: 0,
      influence: 1,
      onPlayFromHand: new GainMoneyEffect(2),
      onPlacedInHeadquarters: Effect.None)
  {
  }
}
