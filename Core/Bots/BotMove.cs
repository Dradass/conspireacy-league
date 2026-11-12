using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Bots;

/// <summary>
/// Ход, который выбрал бот (<see cref="Bot.ChooseMove"/>): одно действие игрока или пас.
/// </summary>
/// <param name="Value">Насколько ход выгоден боту по его оценке (в условных победных очках).</param>
public abstract record BotMove(double Value)
{
  /// <summary>
  /// Ключ хода: по нему бот не повторяет ход, который не удалось выполнить.
  /// </summary>
  public abstract string Key { get; }

  /// <summary>
  /// Что сделал бот — для подсказки на экране.
  /// </summary>
  public abstract string Describe(GameSession session);
}

/// <summary>
/// Купить карточку с рынка.
/// </summary>
public sealed record BuyMove(Cell Cell, PersonCard Card, double Value) : BotMove(Value)
{
  public override string Key => $"buy {Cell}";

  public override string Describe(GameSession session) => $"покупает «{Card.Name}»{TheorySuffix(Card)} за {Card.Cost}$";

  internal static string TheorySuffix(Card card) => card.Theory is { } theory ? $" ({theory.Name})" : string.Empty;
}

/// <summary>
/// Выложить карточку с руки в ячейку — в личный штаб или в общую зону.
/// </summary>
/// <param name="Column">Штаб теории, к которому присоединится карточка, или <c>null</c>.</param>
public sealed record PlaceMove(Card Card, Cell Cell, double Value, TheoryColumn? Column = null) : BotMove(Value)
{
  public override string Key => $"place {Card.GetHashCode()} {Cell}";

  public override string Describe(GameSession session)
  {
    var where = session.Field.CommonZone.Contains(Cell)
      ? (Column ?? session.Field.ColumnAt(Cell)) is { } column ? $"в штаб теории «{column.Theory.Name}»"
        : Card.Theory is { } theory ? $"в общую зону — новая теория «{theory.Name}»" : "в общую зону"
      : "в личный штаб";
    return $"выкладывает «{Card.Name}»{BuyMove.TheorySuffix(Card)} {where}";
  }
}

/// <summary>
/// Выложить карточку с руки взакрытую в теорию по праву своего Спецагента (<see cref="GameSession.PlaceHiddenFromHand"/>).
/// </summary>
public sealed record PlaceHiddenMove(Card Card, TheoryColumn Column, double Value) : BotMove(Value)
{
  public override string Key => $"hidden {Card.GetHashCode()} {Column.Theory.Name}";

  public override string Describe(GameSession session) =>
    $"выкладывает персонажа взакрытую в штаб теории «{Column.Theory.Name}»";
}

/// <summary>
/// Сыграть карточку из личного штаба: вариант её действия и выбранные цели.
/// </summary>
public sealed record UseCardMove(Cell Cell, Effect Option, GameContext Context, double Value) : BotMove(Value)
{
  public override string Key => $"use {Cell} {Option.Description}";

  public override string Describe(GameSession session) =>
    $"играет «{session.Field.CardAt(Cell)?.Name}»: {Option.Description}{Context.TheoriesSuffix()}";
}

/// <summary>
/// Применить свою карточку теории с выбранными целями.
/// </summary>
public sealed record UseTheoryCardMove(OwnedTheoryCard Card, GameContext Context, double Value) : BotMove(Value)
{
  public override string Key => $"theory {Card.Card.Name}";

  public override string Describe(GameSession session) =>
    $"применяет карточку теории «{Card.Card.Name}»: {Card.Card.OnPlayFromHand.Description}{Context.TheoriesSuffix()}";
}

/// <summary>
/// Спасовать до конца раунда.
/// </summary>
public sealed record PassMove() : BotMove(0)
{
  public override string Key => "pass";

  public override string Describe(GameSession session) => "пасует";
}

/// <summary>
/// Выбор бота для действия при выкладывании (<see cref="GameSession.ResolvePlacement"/>): цели или отказ (<c>null</c>).
/// </summary>
public sealed record PlacementResponse(Cell? Target, Cell? Second);

/// <summary>
/// Выбор бота для действия основной теории (<see cref="GameSession.PendingTheoryTrigger"/>). <see cref="Skip"/> — отказаться.
/// </summary>
public sealed record TriggerResponse(bool Skip, Card? HandCard = null, TheoryColumn? Theory = null, Cell? Cell = null);
