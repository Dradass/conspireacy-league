using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Commands;

/// <summary>
/// Итог команды (<see cref="CommandProcessor.Execute"/>).
/// </summary>
/// <param name="Accepted">Команда выполнена.</param>
/// <param name="Error">Почему команда отклонена — для игрока.</param>
/// <param name="Check">Проверка успеха, если команда разыграла карточку.</param>
/// <param name="Fault">
/// Команда прошла все проверки, но упала при выполнении — это ошибка в коде правил, а не в команде.
/// <see cref="Server.ServerGame"/> в этом случае откатывает партию к состоянию до команды.
/// </param>
public sealed record CommandResult(bool Accepted, string? Error = null, SuccessCheck? Check = null, bool Fault = false)
{
  public static CommandResult Ok(SuccessCheck? check = null) => new(true, Check: check);

  public static CommandResult Rejected(string error) => new(false, error);
}

/// <summary>
/// Команда отклонена при подготовке: ссылается на то, чего нет, прислана не тем игроком или нарушает правила.
/// Партия при этом не менялась.
/// </summary>
public sealed class CommandRejectedException(string message) : Exception(message);

/// <summary>
/// Где лежала карточка, на которую команда сослалась идентификатором. По этому месту команду можно повторить
/// в заново собранной партии (<see cref="Server.ServerGame"/>), где у карточек уже другие идентификаторы.
/// </summary>
public abstract record CardLocator;

/// <summary>
/// Карточка в руке игрока, который действует, по номеру в руке.
/// </summary>
public sealed record HandCardLocator(int Index) : CardLocator;

/// <summary>
/// Карточка теории по номеру в <see cref="PlayingField.TheoryCards"/>.
/// </summary>
public sealed record TheoryCardLocator(int Index) : CardLocator;

/// <summary>
/// Проверенная команда, готовая к выполнению (<see cref="CommandProcessor.Prepare"/>).
/// </summary>
public sealed class PreparedCommand
{
  private readonly Func<SuccessCheck?> _run;

  internal PreparedCommand(int seat, GameCommand command, IReadOnlyDictionary<int, CardLocator> locators, Func<SuccessCheck?> run)
  {
    Seat = seat;
    Command = command;
    Locators = locators;
    _run = run;
  }

  /// <summary>
  /// Место игрока, который прислал команду.
  /// </summary>
  public int Seat { get; }

  /// <summary>
  /// Команда.
  /// </summary>
  public GameCommand Command { get; }

  /// <summary>
  /// Где лежали карточки, на которые команда ссылается идентификаторами.
  /// </summary>
  public IReadOnlyDictionary<int, CardLocator> Locators { get; }

  /// <summary>
  /// Выполнить команду. Исключение здесь — ошибка в правилах: все проверки команда уже прошла.
  /// </summary>
  /// <returns>Проверка успеха, если команда разыграла карточку.</returns>
  public SuccessCheck? Run() => _run();
}

/// <summary>
/// Выполняет команды игроков в партии на сервере: проверяет, что команду прислал тот, кто сейчас может действовать,
/// переводит идентификаторы в карточки, теории и игроков и вызывает <see cref="GameSession"/>.
/// </summary>
/// <remarks>
/// Клиенту здесь не доверяют ни в чём. Подготовка команды (<see cref="Prepare"/>) только читает партию: проверяет
/// каждую ссылку и все правила, которые проверило бы выполнение, — неверная команда отклоняется, ничего не меняя.
/// Выполнение (<see cref="PreparedCommand.Run"/>) идёт только для команды, прошедшей все проверки.
/// </remarks>
public sealed class CommandProcessor
{
  /// <summary>
  /// Больше теорий одно действие не выбирает — длиннее список в команде быть не может.
  /// </summary>
  public const int MaxTheoriesPerCommand = 8;

  /// <summary>
  /// Длиннее названия теории не бывает — длиннее строка в команде быть не может.
  /// </summary>
  public const int MaxTheoryNameLength = 128;

  private readonly GameSession _session;

  public CommandProcessor(GameSession session)
  {
    ArgumentNullException.ThrowIfNull(session);
    _session = session;
  }

  /// <summary>
  /// Подготовить и выполнить команду игрока.
  /// </summary>
  /// <param name="seat">Место игрока за столом — по соединению, от которого пришла команда.</param>
  /// <param name="command">Команда.</param>
  public CommandResult Execute(int seat, GameCommand? command)
  {
    PreparedCommand prepared;
    try
    {
      prepared = Prepare(seat, command);
    }
    catch (CommandRejectedException error)
    {
      return CommandResult.Rejected(error.Message);
    }

    try
    {
      return CommandResult.Ok(prepared.Run());
    }
    catch (Exception error)
    {
      // Ошибка в правилах: сервер не падает, но партия могла измениться частично.
      return new CommandResult(false, $"Внутренняя ошибка: {error.Message}", Fault: true);
    }
  }

  /// <summary>
  /// Проверить команду, ничего не меняя в партии.
  /// </summary>
  /// <param name="seat">Место игрока за столом.</param>
  /// <param name="command">Команда.</param>
  /// <param name="replay">
  /// Места карточек из журнала (<see cref="PreparedCommand.Locators"/>), когда команду повторяют в заново собранной
  /// партии; <c>null</c> — карточки ищутся по идентификаторам (<see cref="GameSession.CardIds"/>).
  /// </param>
  /// <exception cref="CommandRejectedException">Команду выполнить нельзя.</exception>
  public PreparedCommand Prepare(int seat, GameCommand? command, IReadOnlyDictionary<int, CardLocator>? replay = null)
  {
    if (command is null)
      throw new CommandRejectedException("Пустая команда.");
    if (seat < 0 || seat >= _session.Players.Count)
      throw new CommandRejectedException("Такого игрока нет за столом.");

    try
    {
      var player = _session.Players[seat];
      Authorize(player, command);
      var preparation = new Preparation(_session, player, replay);
      var run = preparation.Prepare(command);
      return new PreparedCommand(seat, command, preparation.Locators, run);
    }
    catch (Exception error) when (error is InvalidOperationException or ArgumentException)
    {
      // Проверки только читают партию; ошибку в них считаем отказом, а не сбоем.
      throw new CommandRejectedException(error.Message);
    }
  }

  /// <summary>
  /// Может ли игрок прислать эту команду сейчас: выбор для действия основной теории делает её владелец,
  /// всё остальное — игрок, который ходит.
  /// </summary>
  private void Authorize(Player player, GameCommand command)
  {
    if (command is SelectTriggerHandCardCommand or SelectTriggerTheoryCommand or SelectTriggerCellCommand or SkipTheoryTriggerCommand)
    {
      if (_session.PendingTheoryTrigger is not { } trigger)
        throw new CommandRejectedException("Сейчас ничего не выбирают.");
      if (trigger.Owner != player)
        throw new CommandRejectedException("Выбирает владелец основной теории.");
      return;
    }

    if (_session.Phase != GamePhase.PlayerTurns)
      throw new CommandRejectedException("Сейчас не время ходить.");
    if (_session.CurrentPlayer != player)
      throw new CommandRejectedException("Сейчас ходит другой игрок.");
  }

  /// <summary>
  /// Подготовка одной команды: разбор ссылок и проверки. Каждая ветка возвращает выполнение, которое вызывает
  /// метод партии с уже проверенными аргументами.
  /// </summary>
  private sealed class Preparation(GameSession session, Player player, IReadOnlyDictionary<int, CardLocator>? replay)
  {
    private readonly Dictionary<int, CardLocator> _locators = [];

    public IReadOnlyDictionary<int, CardLocator> Locators => _locators;

    private PlayingField Field => session.Field;

    public Func<SuccessCheck?> Prepare(GameCommand command)
    {
      switch (command)
      {
        case BuyCommand buy:
        {
          Require(session.CheckAction());
          Require(session.Market.CheckBuy(buy.Cell, player));
          return Done(() => session.BuyFromMarket(buy.Cell));
        }
        case PlaceCommand place:
        {
          Require(session.CheckAction());
          var card = HandCard(place.CardId);
          Require(Field.Check(card, place.Cell, player));
          return Done(() => session.PlaceFromHand(card, place.Cell));
        }
        case PlaceInWallCommand wall:
        {
          Require(session.CheckAction());
          var card = HandCard(wall.CardId);
          Require(Field.CheckInWall(card, wall.Boundary, player));
          return Done(() => session.PlaceInWallFromHand(card, wall.Boundary));
        }
        case PlaceHiddenCommand hidden:
        {
          Require(session.CheckAction());
          var card = HandCard(hidden.CardId);
          var column = Theory(hidden.Theory);
          Require(Field.CheckHiddenFromHand(card, column, player));
          return Done(() => session.PlaceHiddenFromHand(card, column));
        }
        case ResolvePlacementCommand resolve:
        {
          CheckPlacementTargets(resolve.Target, resolve.Second);
          return Done(() => session.ResolvePlacement(resolve.Target, resolve.Second));
        }
        case UseCardCommand use:
        {
          Require(session.CheckAction());
          var card = Field.CardAt(use.Cell) ?? throw new CommandRejectedException("Здесь нет карточки.");
          var option = Option(card, use.Option);
          var context = Context(use.Targets);
          Require(Field.CheckUse(use.Cell, option, context));
          return () => session.PlayCard(use.Cell, option, context);
        }
        case UseTheoryCardCommand theory:
        {
          Require(session.CheckAction());
          var owned = TheoryCard(theory.CardId);
          var context = Context(theory.Targets);
          Require(Field.CheckTheoryCardUse(owned, context));
          return Done(() => session.UseTheoryCard(owned, context));
        }
        case SelectTriggerHandCardCommand triggerCard:
        {
          var trigger = session.PendingTheoryTrigger!;
          var effect = trigger.Effect;
          var card = HandCard(triggerCard.CardId);
          if (!(effect.RequiresHandCard || effect.AcceptsHandCardTarget))
            throw new CommandRejectedException("Эту карточку выбрать нельзя.");
          if (!effect.RequiresHandCard)
            Require(effect.CheckHandCard(card, trigger.Context));
          return Done(() => session.SelectTriggerHandCard(card));
        }
        case SelectTriggerTheoryCommand triggerTheory:
        {
          var trigger = session.PendingTheoryTrigger!;
          var column = Theory(triggerTheory.Theory);
          if (!trigger.Effect.RequiresTheory || !trigger.Effect.CanTargetTheory(column, trigger.Owner))
            throw new CommandRejectedException($"Теорию «{column}» выбрать нельзя.");
          return Done(() => session.SelectTriggerTheory(column));
        }
        case SelectTriggerCellCommand triggerCell:
        {
          var trigger = session.PendingTheoryTrigger!;
          if (!trigger.Effect.RequiresTargetCell)
            throw new CommandRejectedException("Здесь нужен выбор не карточки на поле.");
          Require(trigger.Effect.CheckTargetCell(triggerCell.Cell, trigger.Context));
          return Done(() => session.SelectTriggerCell(triggerCell.Cell));
        }
        case SkipTheoryTriggerCommand:
          return Done(session.SkipTheoryTrigger);
        case PassCommand:
          Require(session.CheckNoPending());
          return Done(session.Pass);
        default:
          throw new CommandRejectedException("Неизвестная команда.");
      }
    }

    private static Func<SuccessCheck?> Done(Action action) => () =>
    {
      action();
      return null;
    };

    private static Func<SuccessCheck?> Done<T>(Func<T> action) => () =>
    {
      action();
      return null;
    };

    private static void Require(PlacementCheck check)
    {
      if (!check.IsAllowed)
        throw new CommandRejectedException(check.Reason ?? "Так сделать нельзя.");
    }

    /// <summary>
    /// Те же проверки, что делает <see cref="GameSession.ResolvePlacement"/> до выполнения действия.
    /// </summary>
    private void CheckPlacementTargets(Cell? target, Cell? second)
    {
      var choice = session.PendingPlacement ?? throw new CommandRejectedException("Сейчас не выбирают цель для действия выложенной карточки.");
      var effect = choice.Effect;
      if (target is not { } first)
      {
        if (!effect.TargetIsOptional)
          throw new CommandRejectedException($"Для действия «{effect}» нужно выбрать карточку.");
        return;
      }

      var context = session.PlacementContext(choice);
      Require(effect.CheckTargetCell(first, context));
      if (!effect.RequiresSecondTargetCell)
        return;
      if (second is not { } other)
        throw new CommandRejectedException($"Для действия «{effect}» нужно выбрать две карточки.");
      Require(effect.CheckTargetCell(other, context.WithTargetCell(first)));
    }

    /// <summary>
    /// Контекст действия с целями из команды. Каждая ссылка проверяется: чего нет — команда отклоняется.
    /// Подходят ли цели действию, проверяет <see cref="PlayingField.CheckUse"/>.
    /// </summary>
    private GameContext Context(CommandTargets? targets)
    {
      var context = session.CreateContext();
      if (targets is null)
        return context;

      if (targets.Theories is { } theories)
      {
        if (theories.Count > MaxTheoriesPerCommand)
          throw new CommandRejectedException("Слишком много теорий в команде.");
        context = context.WithTargetTheories(theories.Select(Theory).ToList());
      }
      if (targets.Theory is { } theory)
        context = context.WithTargetTheory(Theory(theory));
      if (targets.SourceTheory is { } source)
        context = context.WithSourceTheory(Theory(source));
      if (targets.HandCard is { } handCard)
        context = context.WithHandCard(HandCard(handCard));
      if (targets.TargetCell is { } targetCell)
        context = context.WithTargetCell(targetCell);
      if (targets.SecondTargetCell is { } secondCell)
        context = context.WithSecondTargetCell(secondCell);
      if (targets.AdeptCard is { } adeptCard)
        context = context.WithTargetAdeptCard(adeptCard);
      if (targets.TargetPlayer is { } seat)
      {
        if (seat < 0 || seat >= session.Players.Count)
          throw new CommandRejectedException("Такого игрока нет за столом.");
        context = context.WithTargetPlayer(session.Players[seat]);
      }
      if (targets.BorrowedCell is { } borrowedCell)
        context = context.WithBorrowedCell(borrowedCell);
      if (targets.BorrowedOption is { } borrowedOption)
        context = context.WithBorrowedOption(Option(CardAt(targets.BorrowedCell), borrowedOption));
      if (targets.CopiedCell is { } copiedCell)
        context = context.WithCopiedCell(copiedCell);
      if (targets.CopiedOption is { } copiedOption)
        context = context.WithCopiedOption(Option(CardAt(targets.CopiedCell ?? targets.BorrowedCell), copiedOption));
      return context;
    }

    private Card CardAt(Cell? cell) =>
      (cell is { } value ? Field.CardAt(value) : null) ?? throw new CommandRejectedException("Не выбрана карточка для варианта действия.");

    private static Effect Option(Card card, int index)
    {
      var options = card.OnPlayFromHand.Options;
      return index >= 0 && index < options.Count
        ? options[index]
        : throw new CommandRejectedException($"У карточки «{card.Name}» нет такого варианта действия.");
    }

    /// <summary>
    /// Карточка в руке игрока, который действует. Запоминается её место в руке — для повтора команды.
    /// </summary>
    private Card HandCard(int id)
    {
      var hand = player.Hand.Cards;
      int index;
      if (replay is not null)
        index = replay.GetValueOrDefault(id) is HandCardLocator locator ? locator.Index : -1;
      else
        index = session.CardIds.Find(id) is { } card ? IndexOf(hand, card) : -1;

      if (index < 0 || index >= hand.Count)
        throw new CommandRejectedException("Такой карточки нет в руке.");
      _locators[id] = new HandCardLocator(index);
      return hand[index];
    }

    /// <summary>
    /// Карточка теории. Запоминается её место — для повтора команды.
    /// </summary>
    private OwnedTheoryCard TheoryCard(int id)
    {
      var cards = Field.TheoryCards;
      int index;
      if (replay is not null)
        index = replay.GetValueOrDefault(id) is TheoryCardLocator locator ? locator.Index : -1;
      else
      {
        var card = session.CardIds.Find(id);
        index = -1;
        for (var i = 0; i < cards.Count; i++)
        {
          if (card is not null && ReferenceEquals(cards[i].Card, card))
            index = i;
        }
      }

      if (index < 0 || index >= cards.Count)
        throw new CommandRejectedException("Такой карточки теории нет.");
      _locators[id] = new TheoryCardLocator(index);
      return cards[index];
    }

    private static int IndexOf(IReadOnlyList<Card> cards, Card card)
    {
      for (var i = 0; i < cards.Count; i++)
      {
        if (ReferenceEquals(cards[i], card))
          return i;
      }

      return -1;
    }

    private TheoryColumn Theory(string name)
    {
      if (string.IsNullOrEmpty(name) || name.Length > MaxTheoryNameLength)
        throw new CommandRejectedException("Неверное название теории.");
      return Field.TheoryColumns.FirstOrDefault(column => column.Theory.Name == name)
        ?? throw new CommandRejectedException($"Теории «{name}» нет на поле.");
    }
  }
}
