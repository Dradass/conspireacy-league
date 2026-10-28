using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Passives;

namespace ConspiracyLeague.Core.Cards;

/// <summary>
/// Карточка человека (бездомный, гражданин, профессионал).
/// Её можно разыграть с руки или выложить в штаб теории.
/// </summary>
public abstract class PersonCard : Card
{
  /// <summary>
  /// Вероятность успеха в процентах (0–100).
  /// </summary>
  public int SuccessChancePercent { get; }

  /// <summary>
  /// Стоимость в $.
  /// </summary>
  public int Cost { get; }

  /// <summary>
  /// Действие при выкладывании в штаб теории.
  /// </summary>
  public Effect OnPlacedInHeadquarters { get; }

  protected PersonCard(
    string name,
    int successChancePercent,
    int cost,
    Effect onPlayFromHand,
    Effect onPlacedInHeadquarters,
    PassiveAbility? passive = null)
    : base(name, onPlayFromHand, passive)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(successChancePercent);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(successChancePercent, 100);
    ArgumentOutOfRangeException.ThrowIfNegative(cost);
    SuccessChancePercent = successChancePercent;
    Cost = cost;
    OnPlacedInHeadquarters = onPlacedInHeadquarters;
  }

  /// <summary>
  /// Выложить карточку в штаб теории.
  /// </summary>
  /// <param name="context">Контекст партии.</param>
  public virtual void PlaceInHeadquarters(GameContext context) => OnPlacedInHeadquarters.Execute(context);
}
