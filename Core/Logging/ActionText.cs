using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Logging;

/// <summary>
/// Текст о действии игрока — один для окна истории, журнала партии и истории в сетевой игре. О каждой затронутой
/// карточке сказано, чья она и где лежит («личный штаб игрока Аня», «штаб теории «X», карточка игрока Боря»),
/// о каждой теории — кто её владелец.
/// </summary>
/// <remarks>
/// Скрытое — карту, выложенную взакрытую, карточку, выбранную в руке, лицевую сторону закрытой карты — текст называет,
/// только если <c>revealHidden</c> (журнал сервера). Иначе пишет «закрытая карточка», «карточка из руки».
/// </remarks>
public static class ActionText
{
  /// <summary>
  /// Описать действие: «покупает «Бандит» с рынка.», «разыгрывает «Бандит»: … — карточка «Гражданин» (личный штаб
  /// игрока Аня). Вероятность 65%, выпало 10 — успех.» и т. п. Имя игрока не включается.
  /// </summary>
  /// <param name="action">Действие.</param>
  /// <param name="revealHidden">Называть скрытое.</param>
  /// <param name="alwaysShowMoney">Писать деньги после каждого действия, а не только когда они изменились.</param>
  public static string Describe(PlayerAction action, bool revealHidden, bool alwaysShowMoney = false)
  {
    var actor = action.Player;
    var card = CardName(action.Card, revealHidden);
    var effect = action.Effect is { Length: > 0 } description ? Lowercase(description) : "действие";
    var targets = action.Targets ?? ActionTargets.None;
    var text = action.Kind switch
    {
      PlayerActionKind.Buy => $"покупает {card} с рынка.",
      PlayerActionKind.Place => action.Theory is { } theory
        ? $"выкладывает {card} в штаб теории {TheoryName(theory, actor)}."
        : $"выкладывает {card} в свой личный штаб.",
      PlayerActionKind.PlaceInWall => $"вставляет {card} в перегородку{InTheory(action, actor)}.",
      PlayerActionKind.PlaceHidden => (revealHidden ? $"выкладывает взакрытую {card}" : "выкладывает карточку взакрытую")
        + $"{InTheory(action, actor)}.",
      PlayerActionKind.ResolvePlacement => $"{card} в штабе теории "
        + (action.Theory is { } placed ? TheoryName(placed, actor) : "?") + $": {effect}"
        + (targets.Target is null ? " — без цели." : $"{Targets(targets, actor, revealHidden)}."),
      PlayerActionKind.PlayCard => $"разыгрывает {card}: {effect}{Targets(targets, actor, revealHidden)}"
        + (action.Check is { } check
          ? $". Вероятность {check.Chance}%, выпало {check.Die} — {(check.Succeeded ? "успех" : "провал")}."
          : "."),
      PlayerActionKind.UseTheoryCard => $"применяет карточку теории {card}: {effect}{Targets(targets, actor, revealHidden)}.",
      PlayerActionKind.TriggerHandCard => $"действие основной теории ({effect}): выбрана "
        + (revealHidden ? $"карточка в руке {card}." : "карточка в руке."),
      PlayerActionKind.TriggerTheory => $"действие основной теории ({effect}): выбрана теория "
        + (action.Theory is { } chosen ? TheoryName(chosen, actor) : "?") + ".",
      PlayerActionKind.TriggerCell => $"действие основной теории ({effect}): выбрана "
        + (targets.Target is { } cell ? CardRefText(cell, actor, revealHidden) : card) + ".",
      PlayerActionKind.SkipTrigger => $"отказывается от действия основной теории ({effect}).",
      PlayerActionKind.Pass => "пасует.",
      _ => action.Kind.ToString(),
    };

    var money = actor.Money;
    if (money != action.MoneyBefore)
      return $"{text} Деньги: {action.MoneyBefore}$ → {money}$.";
    return alwaysShowMoney ? $"{text} Деньги: {money}$." : text;
  }

  /// <summary>
  /// Цели действия: « — цель «Гражданин» (личный штаб игрока Аня); теория «X» (владелец — Боря)».
  /// </summary>
  private static string Targets(ActionTargets targets, Player actor, bool revealHidden)
  {
    var parts = new List<string>();
    if (targets.Borrowed is { } borrowed)
      parts.Add($"карточка {CardRefText(borrowed, actor, revealHidden)}");
    if (targets.Copied is { } copied)
      parts.Add($"копия действия {CardRefText(copied, actor, revealHidden)}");
    if (targets.Target is { } target)
      parts.Add($"цель {CardRefText(target, actor, revealHidden)}"
        + (targets.SecondTarget is { } second ? $" и {CardRefText(second, actor, revealHidden)}" : string.Empty));
    if (targets.AdeptCard is { } adepts)
      parts.Add($"адепты на {CardRefText(adepts, actor, revealHidden)}");
    if (targets.HandCard is { } hand)
      parts.Add(revealHidden ? $"карточка из руки {CardName(hand, revealHidden)}" : "карточка из руки");
    if (targets.Player is { } player)
      parts.Add(player == actor ? "игрок — он сам" : $"игрок {player.Name}");

    var theories = targets.Theories ?? [];
    if (targets.SourceTheory is { } source)
      parts.Add($"из теории {TheoryName(source, actor)}" + (theories.Count > 0 ? $" в теорию {TheoryName(theories[0], actor)}" : string.Empty));
    else if (theories.Count == 1)
      parts.Add($"теория {TheoryName(theories[0], actor)}");
    else if (theories.Count > 1)
      parts.Add($"теории {string.Join(", ", theories.Select(theory => TheoryName(theory, actor)))}");

    return parts.Count == 0 ? string.Empty : " — " + string.Join("; ", parts);
  }

  /// <summary>
  /// Карточка на поле: название и где она лежала — «Гражданин» (личный штаб игрока Аня).
  /// </summary>
  public static string CardRefText(CardRef card, Player actor, bool revealHidden)
  {
    var name = card.Card is null ? "пустая ячейка" : CardName(card.Card, revealHidden);
    var whose = card.Owner is not { } owner ? null : owner == actor ? "своя карточка" : $"карточка игрока {owner.Name}";
    var where = card.Theory is { } theory
      ? $"штаб теории «{theory}»{(whose is null ? string.Empty : $", {whose}")}"
      : card.Owner is { } zoneOwner
        ? zoneOwner == actor ? "свой личный штаб" : $"личный штаб игрока {zoneOwner.Name}"
        : "поле";
    return $"{name} ({where})";
  }

  /// <summary>
  /// Теория и её владелец: «X» (своя теория) или «X» (владелец — Боря).
  /// </summary>
  public static string TheoryName(TheoryRef theory, Player actor) =>
    theory.Owner == actor ? $"«{theory.Name}» (своя теория)" : $"«{theory.Name}» (владелец — {theory.Owner.Name})";

  /// <summary>
  /// Название карточки. Закрытая карта, пока её не раскрыли, — без названия (кроме журнала сервера).
  /// </summary>
  public static string CardName(Card? card, bool revealHidden) => card switch
  {
    null => "карточку",
    HiddenCard { IsRevealed: true } revealed => $"«{revealed.Face.Name}» (раскрытая)",
    HiddenCard hidden => revealHidden ? $"«{hidden.Face.Name}» (закрытая)" : "закрытую карточку",
    _ => $"«{card.Name}»",
  };

  private static string InTheory(PlayerAction action, Player actor) =>
    action.Theory is { } theory ? $" в штаб теории {TheoryName(theory, actor)}" : string.Empty;

  private static string Lowercase(string text) =>
    text.Length > 1 && char.IsUpper(text[0]) && !char.IsUpper(text[1]) ? char.ToLowerInvariant(text[0]) + text[1..] : text;
}
