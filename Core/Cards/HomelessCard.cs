using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards;

/// <summary>
/// Бездомный.
/// </summary>
public sealed class HomelessCard : PersonCard
{
  /// <param name="theory">Теория, к которой относится карточка.</param>
  public HomelessCard(Theory? theory = null)
    : base(
      name: "Бездомный",
      successChancePercent: 50,
      cost: 0,
      influence: 1,
      onPlayFromHand: new GainMoneyEffect(2),
      onPlacedInHeadquarters: Effect.None,
      theory: theory)
  {
  }
}
