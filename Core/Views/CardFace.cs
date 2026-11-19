using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Cards.Professionals;
using ConspiracyLeague.Core.Cards.Theories;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Views;

/// <summary>
/// Вид карточки (лицевой стороны).
/// </summary>
public enum CardKind
{
  Homeless,
  Citizen,
  Professional,
  Theory,
}

/// <summary>
/// Лицевая сторона карточки — всё, что на ней видно: по ней клиент рисует карточку и может создать такую же
/// (<see cref="CardFaces.Create"/>). Закрытая карта (<see cref="HiddenCard"/>) лицевой стороной не бывает — её видно
/// как рубашку, а настоящую сторону знают не все (<see cref="CardView.Face"/>).
/// </summary>
/// <param name="Kind">Вид карточки.</param>
/// <param name="Name">Название.</param>
/// <param name="Theory">Название теории или <c>null</c>, если у карточки её нет.</param>
/// <param name="Profession">Профессия профессионала.</param>
/// <param name="Cost">Стоимость, $ (у карточек теорий — 0).</param>
/// <param name="Influence">Влияние сейчас (у карточек теорий — 0).</param>
/// <param name="SuccessChance">Вероятность успеха на карточке, % (у карточек теорий — 0).</param>
public sealed record CardFace(
  CardKind Kind,
  string Name,
  string? Theory,
  Profession? Profession,
  int Cost,
  int Influence,
  int SuccessChance);

/// <summary>
/// Лицевые стороны карточек: из карточки и обратно.
/// </summary>
public static class CardFaces
{
  /// <summary>
  /// Лицевая сторона карточки.
  /// </summary>
  /// <exception cref="ArgumentException">Это закрытая карта — у неё видна только рубашка.</exception>
  public static CardFace Of(Card card) => card switch
  {
    HiddenCard => throw new ArgumentException("У закрытой карты видна только рубашка.", nameof(card)),
    HomelessCard homeless => Person(CardKind.Homeless, homeless, null),
    CitizenCard citizen => Person(CardKind.Citizen, citizen, null),
    GenericProfessionalCard professional => Person(CardKind.Professional, professional, professional.Profession),
    TheoryCard theory => new CardFace(CardKind.Theory, theory.Name, theory.Theory.Name, null, 0, 0, 0),
    _ => throw new ArgumentException($"Неизвестная карточка: {card.GetType().Name}.", nameof(card)),
  };

  /// <summary>
  /// Новая карточка с такой лицевой стороной.
  /// </summary>
  /// <exception cref="ArgumentException">Такой карточки в игре нет.</exception>
  public static Card Create(CardFace face)
  {
    var theory = face.Theory is { } name
      ? TheoryCatalog.All.FirstOrDefault(theory => theory.Name == name) ?? throw new ArgumentException($"Нет теории «{name}».", nameof(face))
      : null;
    return face.Kind switch
    {
      CardKind.Homeless => new HomelessCard(theory),
      CardKind.Citizen => new CitizenCard(theory),
      CardKind.Professional => new GenericProfessionalCard(
        face.Profession ?? throw new ArgumentException("У профессионала нет профессии.", nameof(face)), theory),
      CardKind.Theory => TheoryCardCatalog.Create(theory ?? throw new ArgumentException("У карточки теории нет теории.", nameof(face)))
        ?? throw new ArgumentException($"У теории «{theory.Name}» нет карточки.", nameof(face)),
      _ => throw new ArgumentException($"Неизвестный вид карточки: {face.Kind}.", nameof(face)),
    };
  }

  private static CardFace Person(CardKind kind, PersonCard card, Profession? profession) =>
    new(kind, card.Name, card.Theory?.Name, profession, card.Cost, card.Influence, card.SuccessChancePercent);
}
