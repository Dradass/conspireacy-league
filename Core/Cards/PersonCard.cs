using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

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
  /// Влияние.
  /// </summary>
  public int Influence { get; }

  /// <summary>
  /// Действие при выкладывании в штаб теории.
  /// </summary>
  public Effect OnPlacedInHeadquarters { get; }

  protected PersonCard(
    string name,
    int successChancePercent,
    int cost,
    int influence,
    Effect onPlayFromHand,
    Effect onPlacedInHeadquarters,
    PassiveAbility? passive = null,
    Theory? theory = null)
    : base(name, theory, onPlayFromHand, passive)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(successChancePercent);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(successChancePercent, 100);
    ArgumentOutOfRangeException.ThrowIfNegative(cost);
    ArgumentOutOfRangeException.ThrowIfNegative(influence);
    SuccessChancePercent = successChancePercent;
    Cost = cost;
    Influence = influence;
    OnPlacedInHeadquarters = onPlacedInHeadquarters;
  }

  /// <summary>
  /// Выложить карточку в штаб теории.
  /// </summary>
  /// <param name="context">Контекст партии.</param>
  public virtual void PlaceInHeadquarters(GameContext context) => OnPlacedInHeadquarters.Execute(context);
}
