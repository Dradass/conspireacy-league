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
  public int Influence { get; internal set; }

  /// <summary>
  /// Действие при выкладывании в штаб теории.
  /// </summary>
  public Effect OnPlacedInHeadquarters { get; }

  /// <summary>
  /// Сколько адептов дополнительно приходит, когда их приводят на эту карточку в личном штабе,
  /// или <c>null</c>, если на карточку адептов приводить нельзя.
  /// </summary>
  public virtual int? AdeptsBonus => null;

  /// <summary>
  /// На сколько $ дешевле присоединиться к чужой теории (карточкой без теории).
  /// </summary>
  public virtual int JoiningDiscount => 0;

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
