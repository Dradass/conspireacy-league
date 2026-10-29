using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards;

/// <summary>
/// Бездомный. В личном штабе снижает вероятность успеха соседних персонажей (<see cref="NeighborSuccessPenalty"/>).
/// </summary>
public sealed class HomelessCard : PersonCard
{
  /// <summary>
  /// На сколько процентов бездомный в личном штабе снижает вероятность успеха соседних персонажей.
  /// </summary>
  public const int NeighborSuccessPenalty = 5;

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
