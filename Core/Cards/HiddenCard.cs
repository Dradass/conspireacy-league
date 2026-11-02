using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Cards;

/// <summary>
/// Закрытая карточка: карточка с руки, выложенная в штаб теории рубашкой вверх (<see cref="Effects.PlaceHiddenCardEffect"/>).
/// Другие игроки не знают, что это за карточка; по правилам она считается гражданином без теории —
/// у неё его вероятность успеха, стоимость, влияние и действия.
/// </summary>
public sealed class HiddenCard : PersonCard
{
  /// <summary>
  /// Настоящая карточка. Пока карточка не раскрыта, её видят только владелец и те, кто в неё посмотрел
  /// (<see cref="IsKnownBy"/>).
  /// </summary>
  public Card Face { get; }

  /// <summary>
  /// Карточку раскрыли (<see cref="Effects.RevealHiddenCardEffect"/>): её настоящую лицевую сторону видят все.
  /// По правилам она по-прежнему гражданин без теории.
  /// </summary>
  public bool IsRevealed { get; private set; }

  private readonly HashSet<Player> _knownBy = [];

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

  internal void Reveal() => IsRevealed = true;

  /// <summary>
  /// Посмотрел ли игрок в карточку (<see cref="Effects.PeekHiddenCardEffect"/>): тогда он, как и владелец,
  /// может смотреть её в любой момент.
  /// </summary>
  public bool IsKnownBy(Player player) => _knownBy.Contains(player);

  internal void ShowTo(Player player) => _knownBy.Add(player);
}
