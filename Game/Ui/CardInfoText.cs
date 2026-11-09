using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Cards.Professionals;
using ConspiracyLeague.Core.Cards.Theories;
using ConspiracyLeague.Core.Decks;
using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Текст окна информации о карточке в BBCode. Ссылка на теорию — <c>theory:N</c>, где N — индекс в <see cref="TheoryCatalog.All"/>.
/// </summary>
public static class CardInfoText
{
  /// <summary>
  /// Текст закрытой карты, которую игрок не видит.
  /// </summary>
  public static string Hidden => Lines(Title("Закрытая карта"), "Характеристики скрыты.");

  /// <summary>
  /// Текст теории: для основной — её эффект и подтеории, для побочной — эффект и основная теория.
  /// Названия теорий кликабельны.
  /// </summary>
  public static string TheoryText(Theory theory)
  {
    var lines = new List<string> { Title(theory.Name) };

    var card = TheoryCardCatalog.Create(theory);
    if (card is null)
      lines.Add("У этой теории нет карточки с эффектом.");
    else
    {
      var effects = EffectLines(card.OnPlayFromHand, card.Passive).ToList();
      lines.AddRange(effects.Count > 0 ? effects : ["Действия нет."]);
    }

    if (theory.Parent is { } parent)
    {
      lines.Add("");
      lines.Add($"[b]Основная теория:[/b] {Link(parent)}");
    }
    else
    {
      var subtheories = TheoryCatalog.All.Where(sub => sub.Parent == theory).ToList();
      if (subtheories.Count > 0)
      {
        lines.Add("");
        lines.Add("[b]Подтеории:[/b]");
        lines.AddRange(subtheories.Select(sub => $"• {Link(sub)}"));
      }
    }

    return Lines(lines.ToArray());
  }

  /// <summary>
  /// Текст персонажа: имя, теория, стоимость, количество в игре, шанс успеха и все эффекты.
  /// </summary>
  /// <param name="person">Персонаж (лицевая сторона).</param>
  /// <param name="currentChance">Текущая вероятность успеха в процентах.</param>
  public static string PersonText(PersonCard person, int currentChance)
  {
    var lines = new List<string>
    {
      Title(person.Name),
      person.Theory is { } theory ? $"Теория: {Link(theory)}" : "Без теории",
      "",
      $"Стоимость: {person.Cost}$",
      $"В игре: {CountInGame(person)}",
      $"Шанс успеха: базовый {person.SuccessChancePercent}%, текущий {currentChance}%",
      "",
      "[b]В личном штабе:[/b]",
    };
    lines.AddRange(Bullets(PersonalEffects(person)));
    lines.Add("");
    lines.Add("[b]В штабе теории:[/b]");
    lines.AddRange(Bullets(TheoryEffects(person)));
    return Lines(lines.ToArray());
  }

  /// <summary>
  /// Эффекты персонажа в личном штабе: действие, пассивные способности и адепты.
  /// Пассивная способность полицейского работает в штабе теории и сюда не входит.
  /// </summary>
  private static IEnumerable<string> PersonalEffects(PersonCard person)
  {
    if (!ReferenceEquals(person.OnPlayFromHand, Effect.None))
      yield return $"Действие: {Escape(person.OnPlayFromHand.Description)}";
    if (person.Passive is not PolicemanPassive && !ReferenceEquals(person.Passive, PassiveAbility.None))
      yield return $"Пассивно: {Escape(person.Passive.Description)}";
    if (person is HomelessCard)
      yield return $"Пассивно: соседи в личном штабе −{HomelessCard.NeighborSuccessPenalty}% успеха";
    if (person.AdeptsBonus is not null)
      yield return "На карточку в личном штабе можно приводить адептов";
    if (person.Passive is PolicemanPassive)
      yield return PolicemanTollText;
  }

  /// <summary>
  /// Плата владельцу полицейского за действие, применённое к нему другим игроком.
  /// </summary>
  private static string PolicemanTollText =>
    $"Пассивно: другой игрок, применяющий к полицейскому действие, платит его владельцу {ConspiracyLeague.Core.Field.PlayingField.PolicemanToll}$";

  /// <summary>
  /// Эффекты персонажа в штабе теории: при выкладывании, пассивная способность полицейского и скидка при присоединении.
  /// </summary>
  private static IEnumerable<string> TheoryEffects(PersonCard person)
  {
    if (!ReferenceEquals(person.OnPlacedInHeadquarters, Effect.None))
      yield return $"При выкладывании: {Escape(person.OnPlacedInHeadquarters.Description)}";
    if (person.Passive is PolicemanPassive)
      yield return $"Пассивно: {Escape(person.Passive.Description)}";
    if (person.Passive is PolicemanPassive)
      yield return PolicemanTollText;
    if (person.JoiningDiscount > 0)
      yield return $"Присоединение к теории дешевле на {person.JoiningDiscount}$";
  }

  private static IEnumerable<string> Bullets(IEnumerable<string> effects)
  {
    var list = effects.ToList();
    return list.Count == 0 ? ["Эффектов нет."] : list.Select(effect => $"• {effect}");
  }

  /// <summary>
  /// Краткий эффект теории (карточки теории) для подсказки: действие и пассивная способность, в BBCode.
  /// </summary>
  public static string TheoryEffectSummary(Theory theory)
  {
    var card = TheoryCardCatalog.Create(theory);
    if (card is null)
      return "У этой теории нет карточки с эффектом.";

    var effects = EffectLines(card.OnPlayFromHand, card.Passive).ToList();
    return effects.Count > 0 ? Lines(effects.ToArray()) : "Действия нет.";
  }

  /// <summary>
  /// Строки с действием с руки и пассивным действием; пустые, если их нет.
  /// </summary>
  private static IEnumerable<string> EffectLines(Effect onPlay, PassiveAbility passive)
  {
    if (!ReferenceEquals(onPlay, Effect.None))
      yield return $"С руки: {Escape(onPlay.Description)}";
    if (!ReferenceEquals(passive, PassiveAbility.None))
      yield return $"Пассивно: {Escape(passive.Description)}";
  }

  private static int CountInGame(PersonCard person) => person switch
  {
    HomelessCard => GameCardSet.CountOf(PersonKind.Homeless),
    CitizenCard => GameCardSet.CountOf(PersonKind.Citizen),
    GenericProfessionalCard professional => GameCardSet.CountOf(PersonKind.Professional, professional.Profession),
    _ => 0,
  };

  private static string Link(Theory theory) =>
    $"[url=theory:{TheoryCatalog.All.ToList().IndexOf(theory)}]{Escape(theory.Name)}[/url]";

  private static string Title(string name) => $"[font={UiStyle.TitleFontPath}][font_size=30]{Escape(name)}[/font_size][/font]";

  private static string Lines(params string[] lines) => string.Join("\n", lines);

  private static string Escape(string text) => text.Replace("[", "[lb]");
}
