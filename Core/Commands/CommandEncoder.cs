using ConspiracyLeague.Core.Bots;
using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Commands;

/// <summary>
/// Переводит действия игрока из объектов партии (карточки, контекст с целями, ходы ботов) в команды
/// (<see cref="GameCommand"/>) — обратное к <see cref="CommandProcessor"/>.
/// </summary>
public static class CommandEncoder
{
  /// <summary>
  /// Цели из контекста действия.
  /// </summary>
  public static CommandTargets Targets(GameSession session, GameContext context)
  {
    var field = session.Field;
    int? OptionIndex(Cell? cell, Effect? option) =>
      option is null ? null
      : cell is { } value && field.CardAt(value) is { } card && IndexOf(card, option) is var index && index >= 0 ? index
      : throw new ArgumentException($"Вариант действия «{option}» не принадлежит выбранной карточке.", nameof(context));

    return new CommandTargets
    {
      Theories = context.TargetTheories.Count > 0 ? context.TargetTheories.Select(column => column.Theory.Name).ToList() : null,
      Theory = context.TargetTheory?.Theory.Name,
      SourceTheory = context.SourceTheory?.Theory.Name,
      HandCard = context.HandCard is { } handCard ? session.CardIds.IdOf(handCard) : null,
      TargetCell = context.TargetCell,
      SecondTargetCell = context.SecondTargetCell,
      AdeptCard = context.TargetAdeptCard,
      TargetPlayer = context.TargetPlayer is { } player ? IndexOfPlayer(session, player) : null,
      BorrowedCell = context.BorrowedCell,
      BorrowedOption = OptionIndex(context.BorrowedCell, context.BorrowedOption),
      CopiedCell = context.CopiedCell,
      CopiedOption = OptionIndex(context.CopiedCell ?? context.BorrowedCell, context.CopiedOption),
    };
  }

  /// <summary>
  /// Сыграть карточку из личного штаба.
  /// </summary>
  public static UseCardCommand UseCard(GameSession session, Cell cell, Effect option, GameContext context)
  {
    var card = session.Field.CardAt(cell) ?? throw new ArgumentException("Здесь нет карточки.", nameof(cell));
    var index = IndexOf(card, option);
    if (index < 0)
      throw new ArgumentException($"У карточки «{card}» нет действия «{option}».", nameof(option));
    return new UseCardCommand(cell, index, Targets(session, context));
  }

  /// <summary>
  /// Ход бота (<see cref="Bot.ChooseMove"/>).
  /// </summary>
  public static GameCommand FromMove(GameSession session, BotMove move) => move switch
  {
    BuyMove buy => new BuyCommand(buy.Cell),
    PlaceMove place => new PlaceCommand(session.CardIds.IdOf(place.Card), place.Cell),
    PlaceHiddenMove hidden => new PlaceHiddenCommand(session.CardIds.IdOf(hidden.Card), hidden.Column.Theory.Name),
    UseCardMove use => UseCard(session, use.Cell, use.Option, use.Context),
    UseTheoryCardMove theory => new UseTheoryCardCommand(session.CardIds.IdOf(theory.Card.Card), Targets(session, theory.Context)),
    PassMove => new PassCommand(),
    _ => throw new ArgumentException($"Неизвестный ход бота: {move}.", nameof(move)),
  };

  /// <summary>
  /// Ответ бота на выбор цели для выложенной карточки (<see cref="Bot.ChoosePlacement"/>).
  /// </summary>
  public static ResolvePlacementCommand FromPlacement(PlacementResponse response) => new(response.Target, response.Second);

  /// <summary>
  /// Ответ бота для действия основной теории (<see cref="Bot.ChooseTrigger"/>) — по одной команде на каждый выбор,
  /// как их делает игрок.
  /// </summary>
  public static IEnumerable<GameCommand> FromTrigger(GameSession session, TriggerResponse response)
  {
    if (response.Skip)
    {
      yield return new SkipTheoryTriggerCommand();
      yield break;
    }

    if (response.HandCard is { } card)
      yield return new SelectTriggerHandCardCommand(session.CardIds.IdOf(card));
    if (response.Theory is { } theory)
      yield return new SelectTriggerTheoryCommand(theory.Theory.Name);
    if (response.Cell is { } cell)
      yield return new SelectTriggerCellCommand(cell);
  }

  private static int IndexOf(Card card, Effect option)
  {
    var options = card.OnPlayFromHand.Options;
    for (var i = 0; i < options.Count; i++)
    {
      if (ReferenceEquals(options[i], option))
        return i;
    }

    return -1;
  }

  private static int IndexOfPlayer(GameSession session, Player player)
  {
    for (var i = 0; i < session.Players.Count; i++)
    {
      if (session.Players[i] == player)
        return i;
    }

    throw new ArgumentException($"Игрока {player} нет за столом.", nameof(player));
  }
}
