namespace ConspiracyLeague.Core.Cards;

/// <summary>
/// Закрытая карточка: карточка с руки, выложенная в штаб теории рубашкой вверх (<see cref="Effects.PlaceHiddenCardEffect"/>).
/// Другие игроки не знают, что это за карточка; по правилам она считается гражданином без теории —
/// у неё его вероятность успеха, стоимость, влияние и действия.
/// </summary>
public sealed class HiddenCard : PersonCard
{
  /// <summary>
  /// Настоящая карточка. Её видит только владелец.
  /// </summary>
  public Card Face { get; }

  /// <param name="face">Настоящая карточка.</param>
  public HiddenCard(Card face)
    : this(face, new CitizenCard())
  {
  }

  private HiddenCard(Card face, CitizenCard citizen)
    : base(
      name: "Закрытая карта",
      successChancePercent: citizen.SuccessChancePercent,
      cost: citizen.Cost,
      influence: citizen.Influence,
      onPlayFromHand: citizen.OnPlayFromHand,
      onPlacedInHeadquarters: citizen.OnPlacedInHeadquarters,
      passive: citizen.Passive)
  {
    ArgumentNullException.ThrowIfNull(face);
    Face = face;
  }
}
