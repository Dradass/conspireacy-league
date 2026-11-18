using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Decks;
using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Passives;

namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Сессия игры — всё состояние партии: игроки и их балансы, очерёдность ходов, поле с личными штабами
/// и выложенные теории (какие карточки в каждой теории, кто их владельцы, сколько адептов).
/// </summary>
/// <remarks>
/// Партия идёт по фазам (<see cref="GamePhase"/>): подготовка (<see cref="Start"/>), затем раунды.
/// Раунд: игроки ходят, пока все не спасуют (<see cref="Pass"/>). За ход можно сделать <see cref="ActionsPerTurn"/>
/// действия — купить карточку, выложить её на поле, сыграть карточку; после последнего ход сам переходит
/// к следующему игроку. Игрок, которому нечем ходить (<see cref="HasMoves"/>), пасует автоматически. Затем закрываются несостоятельные штабы
/// (<see cref="CloseInsolventTheories"/>), адепты приносят деньги (<see cref="PayNextTheoryIncome"/>),
/// сыгранные карточки переворачиваются лицом вверх (<see cref="RefreshPlayedCards"/>), обновляется рынок (<see cref="RefreshMarket"/>), и начинается следующий раунд (<see cref="StartNextRound"/>).
/// Если при обновлении рынка в колоде не осталось карточек, идёт итоговый подсчёт: владельцы действующих теорий
/// получают деньги за лидеров (<see cref="PayNextLeadersBonus"/>), адепты ещё раз приносят деньги
/// (<see cref="PayNextTheoryIncome"/>), и партия завершена (<see cref="Ranking"/>).
/// Шаги конца раунда вызываются по одному, чтобы между ними можно было показать, что произошло.
/// </remarks>
public sealed class GameSession
{
  /// <summary>
  /// Сколько $ у каждого игрока в начале партии.
  /// </summary>
  public const int StartingMoney = 10;

  /// <summary>
  /// Сколько действий можно сделать за ход: купить карточку, выложить её на поле, сыграть карточку.
  /// </summary>
  public const int ActionsPerTurn = 2;

  private readonly Queue<TheoryColumn> _unpaidTheories = new();
  private readonly List<TheoryIncome> _incomeOfRound = [];
  private readonly Random _random;

  /// <summary>
  /// Режим партии: как определяется победитель.
  /// </summary>
  public GameMode Mode { get; }

  /// <summary>
  /// Игроки и очерёдность их ходов.
  /// </summary>
  public TurnOrder Turns { get; }

  /// <summary>
  /// Игровое поле.
  /// </summary>
  public PlayingField Field { get; }

  /// <summary>
  /// Рынок: общая колода и открытые карточки.
  /// </summary>
  public Market Market { get; }

  /// <summary>
  /// Постоянные идентификаторы карточек: по ним на карточки ссылаются команды по сети.
  /// </summary>
  public CardRegistry CardIds { get; } = new();

  /// <summary>
  /// Игроки в порядке хода.
  /// </summary>
  public IReadOnlyList<Player> Players => Turns.Players;

  /// <summary>
  /// Игрок, который сейчас ходит.
  /// </summary>
  public Player CurrentPlayer => Turns.CurrentPlayer;

  /// <summary>
  /// Выложенные теории.
  /// </summary>
  public IReadOnlyList<TheoryColumn> Theories => Field.TheoryColumns;

  /// <summary>
  /// Текущая фаза партии.
  /// </summary>
  public GamePhase Phase { get; private set; } = GamePhase.Setup;

  /// <summary>
  /// Номер раунда, с единицы; 0 — партия ещё не началась.
  /// </summary>
  public int Round { get; private set; }

  /// <summary>
  /// Сколько действий осталось у игрока, который сейчас ходит.
  /// </summary>
  public int ActionsLeft { get; private set; }

  /// <summary>
  /// Проверка успеха карточки, которую сейчас играют (<see cref="CheckSuccess"/>), или <c>null</c>.
  /// Пока она не завершена (<see cref="UseCard"/>), других действий сделать нельзя.
  /// </summary>
  public SuccessCheck? PendingCheck { get; private set; }

  /// <summary>
  /// Началась или завершилась проверка успеха; аргумент — <see cref="PendingCheck"/>.
  /// </summary>
  public event Action<SuccessCheck?>? PendingCheckChanged;

  /// <summary>
  /// Выбор цели для действия карточки, только что выложенной в штаб теории (<see cref="PlaceFromHand"/>),
  /// или <c>null</c>. Пока он не завершён (<see cref="ResolvePlacement"/>), других действий сделать нельзя.
  /// </summary>
  public PlacementChoice? PendingPlacement { get; private set; }

  /// <summary>
  /// Начался или завершился выбор цели для действия при выкладывании; аргумент — <see cref="PendingPlacement"/>.
  /// </summary>
  public event Action<PlacementChoice?>? PendingPlacementChanged;

  /// <summary>
  /// Изменилось количество оставшихся действий; аргумент — сколько осталось.
  /// </summary>
  public event Action<int>? ActionsLeftChanged;

  /// <summary>
  /// Игрок спасовал автоматически — ходить ему нечем; аргумент — этот игрок.
  /// </summary>
  public event Action<Player>? AutoPassed;

  /// <summary>
  /// Фаза партии сменилась; аргумент — новая фаза.
  /// </summary>
  public event Action<GamePhase>? PhaseChanged;

  /// <summary>
  /// Режим гонки: текущий раунд — последний, потому что теория дошла до финиша (<see cref="RaceScoring.FinishLeaders(int)"/>).
  /// Не снимается, даже если потом из теории уйдёт лидер.
  /// </summary>
  public bool IsLastRound { get; private set; }

  /// <summary>
  /// Режим гонки: теория первой дошла до финиша, и раунд объявлен последним; аргумент — эта теория.
  /// </summary>
  public event Action<TheoryColumn>? LastRoundDeclared;

  /// <summary>
  /// Режим гонки: сколько лидеров нужно штабу теории, чтобы дойти до финиша.
  /// </summary>
  public int FinishLeaders => RaceScoring.FinishLeaders(Players.Count);

  /// <summary>
  /// Подготовить партию: поле стандартного размера, личные штабы игроков стоят в ряд внизу поля,
  /// перемешанная общая колода (<see cref="MarketDeck"/>) лежит на рынке.
  /// </summary>
  /// <param name="players">Игроки в порядке хода.</param>
  /// <param name="random">Источник случайности; по умолчанию — общий.</param>
  /// <param name="mode">Режим партии: как определяется победитель (<see cref="Ranking"/>).</param>
  public GameSession(IEnumerable<Player> players, Random? random = null, GameMode mode = GameMode.TheoryDevelopment)
  {
    Mode = mode;
    Turns = new TurnOrder(players);
    Field = new PlayingField(FieldLayout.Columns, FieldLayout.Rows, FieldLayout.CommonZone(Players.Count));
    for (var i = 0; i < Players.Count; i++)
      Field.AddPlayer(Players[i], FieldLayout.PlayerZone(i, Players.Count));
    _random = random ?? Random.Shared;
    var (marketDeck, marketReserve) = MarketDeck.CreateWithReserve(Players.Count, _random);
    Market = new Market(FieldLayout.MarketZone, marketDeck, marketReserve);
    // Подписываемся первыми: к моменту, когда о новом ходе узнают остальные, действия уже восстановлены.
    Turns.TurnChanged += _ => SetActionsLeft(ActionsPerTurn);
  }

  /// <summary>
  /// Сколько карточек открыто в каждой строке рынка: по две на игрока, всего на рынке 4N.
  /// </summary>
  public int MarketCardsPerRow => Players.Count * 2;

  /// <summary>
  /// Подготовка и начало первого раунда: каждый игрок получает стартовую руку (<see cref="StartingHand"/>) —
  /// новых бездомных и граждан со случайными теориями, на рынке открывается 4N карточек: в первую и третью строки по <see cref="MarketCardsPerRow"/>.
  /// </summary>
  public void Start()
  {
    RequirePhase(GamePhase.Setup);
    foreach (var player in Players)
    {
      foreach (var card in StartingHand.Create(_random))
        player.Hand.Add(card);
    }

    Market.OpenStartingCards(MarketCardsPerRow);
    StartNextRound();
  }

  /// <summary>
  /// Игрок, который сейчас ходит, покупает карточку с рынка в руку. Это действие.
  /// </summary>
  /// <param name="cell">Ячейка рынка.</param>
  /// <returns>Купленная карточка.</returns>
  public PersonCard BuyFromMarket(Cell cell)
  {
    RequireAction();
    var player = CurrentPlayer;
    var money = player.Money;
    var card = Market.Buy(cell, player);
    CompleteAction();
    Report(new PlayerAction(player, PlayerActionKind.Buy, money, card, cell));
    return card;
  }

  /// <summary>
  /// Игрок сделал действие — для журнала партии. Вызывается после того, как действие выполнено.
  /// </summary>
  public event Action<PlayerAction>? ActionTaken;

  private void Report(PlayerAction action) => ActionTaken?.Invoke(action);

  /// <summary>
  /// Карточка в ячейке, как она лежит сейчас: чья и где. Для журнала — запоминается до действия.
  /// </summary>
  private CardRef? RefAt(Cell? cell)
  {
    if (cell is not { } at)
      return null;
    var column = Field.ColumnAt(at);
    var owner = column?.OwnerAt(at) ?? Field.PlayerZones.FirstOrDefault(pair => pair.Value.Contains(at)).Key;
    return new CardRef(at, Field.CardAt(at), owner, column?.Theory.Name);
  }

  private static TheoryRef RefOf(TheoryColumn column) => new(column.Theory.Name, column.Owner);

  /// <summary>
  /// Цели действия, как они выглядят сейчас (до выполнения).
  /// </summary>
  /// <param name="context">Контекст с выбранными целями.</param>
  /// <param name="except">Теория, которую не нужно упоминать среди целей (например, штаб выложенной карточки).</param>
  private ActionTargets TargetsOf(GameContext context, TheoryColumn? except = null)
  {
    var theories = context.TargetTheories
      .Concat(context.TargetTheory is { } single ? [single] : [])
      .Where(column => column != except && column != context.SourceTheory)
      .Distinct()
      .Select(RefOf)
      .ToList();
    return new ActionTargets(
      RefAt(context.TargetCell),
      RefAt(context.SecondTargetCell),
      RefAt(context.BorrowedCell),
      RefAt(context.CopiedCell),
      RefAt(context.TargetAdeptCard),
      context.TargetPlayer,
      context.HandCard,
      context.SourceTheory is { } source && source != except ? RefOf(source) : null,
      theories);
  }

  /// <summary>
  /// Игрок, который сейчас ходит, выкладывает карточку с руки на поле — в личный штаб или в общую зону. Это действие.
  /// Если у карточки в штабе теории есть действие с выбором цели и выбрать есть что, действие завершается
  /// только после выбора (<see cref="PendingPlacement"/>, <see cref="ResolvePlacement"/>).
  /// </summary>
  /// <returns>Условия, на которых карточка положена.</returns>
  /// <exception cref="InvalidOperationException">Карточки нет в руке или её сюда класть нельзя.</exception>
  /// <summary>
  /// Режим разработчика: положить карточку в руку игрока без действия и оплаты.
  /// </summary>
  public void AddToHandForDeveloper(Player player, Card card) => player.Hand.Add(card);

  public PlacementCheck PlaceFromHand(Card card, Cell cell) =>
    PlaceCardFromHand(card, PlayerActionKind.Place, () => Field.Place(card, cell, CurrentPlayer));

  /// <summary>
  /// Игрок, который сейчас ходит, вставляет дочернюю теорию с руки в перегородку между столбцами
  /// (см. <see cref="PlayingField.CheckInWall"/>). Это действие.
  /// </summary>
  /// <returns>Условия, на которых карточка положена.</returns>
  /// <exception cref="InvalidOperationException">Карточки нет в руке или её сюда класть нельзя.</exception>
  public PlacementCheck PlaceInWallFromHand(Card card, int boundary) =>
    PlaceCardFromHand(card, PlayerActionKind.PlaceInWall, () => Field.PlaceInWall(card, boundary, CurrentPlayer));

  /// <summary>
  /// Игрок, который сейчас ходит, выкладывает карточку с руки взакрытую сверху стопки теории — по праву своего
  /// Спецагента в её штабе (<see cref="PlayingField.CheckHiddenFromHand"/>). Это действие хода. Платят, как за
  /// закрытую карту; действия при выкладывании у закрытой карты нет.
  /// </summary>
  /// <returns>Условия, на которых карточка положена.</returns>
  /// <exception cref="InvalidOperationException">Карточки нет в руке или её сюда класть нельзя.</exception>
  public PlacementCheck PlaceHiddenFromHand(Card card, TheoryColumn column)
  {
    RequireAction();
    var hand = CurrentPlayer.Hand;
    if (!hand.Contains(card))
      throw new InvalidOperationException($"Карточки «{card}» нет в руке игрока {CurrentPlayer}.");
    var check = Field.CheckHiddenFromHand(card, column, CurrentPlayer);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    var player = CurrentPlayer;
    var money = player.Money;
    var theory = RefOf(column);
    using var scope = CurrentPlayer.EarningFrom($"Выкладывание взакрытую: {card.Name}");
    var placed = Field.PlaceHidden(card, column, CurrentPlayer);
    hand.Remove(card);
    CompleteAction();
    Report(new PlayerAction(player, PlayerActionKind.PlaceHidden, money, card, Field.CellOf(card) ?? column.Top, theory));
    return placed;
  }

  private PlacementCheck PlaceCardFromHand(Card card, PlayerActionKind kind, Func<PlacementCheck> place)
  {
    RequireAction();
    var player = CurrentPlayer;
    var money = player.Money;
    // Владельцы теорий до выкладывания: в чужой ли штаб кладут карточку.
    var owners = Field.TheoryColumns.ToDictionary(column => column.Theory.Name, column => column.Owner);
    TheoryRef? TheoryOf(TheoryColumn? column) =>
      column is null ? null : new TheoryRef(column.Theory.Name, owners.GetValueOrDefault(column.Theory.Name) ?? column.Owner);
    var hand = CurrentPlayer.Hand;
    if (!hand.Contains(card))
      throw new InvalidOperationException($"Карточки «{card}» нет в руке игрока {CurrentPlayer}.");

    using var scope = CurrentPlayer.EarningFrom($"Выкладывание: {card.Name}");
    PlacementCheck check;
    try
    {
      check = place();
    }
    catch
    {
      // Карточка уже легла на поле, а потом что-то отказало (например, действие при выкладывании):
      // она не должна остаться ещё и в руке, а выкладывание — потраченное действие хода.
      if (Field.CellOf(card) is not null)
      {
        hand.Remove(card);
        CompleteAction();
      }
      throw;
    }
    // Выложенная теория могла сдвинуться в соседний столбец вместе с карточкой — берём её актуальную ячейку.
    var placedCell = Field.CellOf(card) ?? throw new InvalidOperationException("Карточка не легла на поле.");
    hand.Remove(card);
    var placedIn = Field.ColumnAt(placedCell);
    if (placedIn is { } theory && card is PersonCard { OnPlacedInHeadquarters: { RequiresTargetCell: true } effect })
    {
      var choice = new PlacementChoice(placedCell, effect, theory);
      if (effect.TargetCells(PlacementContext(choice)).Any(target => target.Check.IsAllowed))
      {
        PendingPlacement = choice;
        PendingPlacementChanged?.Invoke(choice);
        Report(new PlayerAction(player, kind, money, card, placedCell, TheoryOf(theory)));
        return check;
      }

      // Выбрать нечего — действие выполняется без цели (например, получить деньги).
      effect.Execute(PlacementContext(choice));
    }

    CompleteAction();
    Report(new PlayerAction(player, kind, money, card, placedCell, TheoryOf(placedIn)));
    return check;
  }

  /// <summary>
  /// Контекст для действия карточки, выложенной в штаб теории: штаб и сама карточка.
  /// </summary>
  public GameContext PlacementContext(PlacementChoice choice) =>
    CreateContext().WithTargetTheory(choice.Theory).WithSourceCell(choice.Cell);

  /// <summary>
  /// Завершить выбор цели для действия карточки, выложенной в штаб теории (<see cref="PendingPlacement"/>):
  /// выполнить действие с выбранными карточками или без них (игрок отказался) и завершить действие хода.
  /// </summary>
  /// <param name="target">Выбранная карточка или <c>null</c>, если игрок отказался.</param>
  /// <param name="second">Вторая карточка (<see cref="Effect.RequiresSecondTargetCell"/>).</param>
  /// <exception cref="InvalidOperationException">Выбора нет или карточки выбрать нельзя.</exception>
  public void ResolvePlacement(Cell? target = null, Cell? second = null)
  {
    var choice = PendingPlacement ?? throw new InvalidOperationException("Сейчас не выбирают цель для действия выложенной карточки.");
    var player = CurrentPlayer;
    var money = player.Money;
    var effect = choice.Effect;
    var context = PlacementContext(choice);
    if (target is { } first)
    {
      context = context.WithTargetCell(RequireTarget(effect, first, context));
      if (effect.RequiresSecondTargetCell)
      {
        if (second is not { } other)
          throw new ArgumentException($"Для действия «{effect}» нужно выбрать две карточки.", nameof(second));
        context = context.WithSecondTargetCell(RequireTarget(effect, other, context));
      }
    }
    else if (!effect.TargetIsOptional)
      throw new ArgumentException($"Для действия «{effect}» нужно выбрать карточку.", nameof(target));

    var targets = TargetsOf(context, except: choice.Theory);
    var placedCard = Field.CardAt(choice.Cell);
    var placedTheory = RefOf(choice.Theory);
    PendingPlacement = null;
    PendingPlacementChanged?.Invoke(null);
    // Цель принята: даже если действие откажет на полпути, выкладывание карточки — потраченное действие хода.
    try
    {
      Field.PayPolicemanTolls(effect, context);
      effect.Execute(context);
    }
    finally
    {
      CompleteAction();
    }

    Report(new PlayerAction(player, PlayerActionKind.ResolvePlacement, money, placedCard, choice.Cell, placedTheory,
      effect.Description, targets));
  }

  private static Cell RequireTarget(Effect effect, Cell cell, GameContext context)
  {
    var check = effect.CheckTargetCell(cell, context);
    return check.IsAllowed ? cell : throw new InvalidOperationException(check.Reason);
  }

  /// <summary>
  /// Игрок, который сейчас ходит, начинает играть карточку из своего личного штаба: проводится проверка успеха
  /// (<see cref="SuccessCheck"/>). Розыгрыш завершается вызовом <see cref="UseCard"/>.
  /// </summary>
  /// <param name="cell">Ячейка карточки.</param>
  /// <returns>Проверка успеха.</returns>
  /// <exception cref="InvalidOperationException">Карточку нельзя использовать.</exception>
  public SuccessCheck CheckSuccess(Cell cell)
  {
    RequireAction();
    if (!Field.CanUse(cell, CurrentPlayer))
      throw new InvalidOperationException("Эту карточку нельзя использовать.");

    var check = SuccessCheck.Perform(cell, Field.SuccessChanceAt(cell), _random);
    SetPendingCheck(check);
    return check;
  }

  /// <summary>
  /// Сыграть карточку из личного штаба одним шагом: сначала проверить весь розыгрыш — карточку, вариант действия и
  /// цели (<see cref="PlayingField.CheckUse"/>), — и только потом бросить кубик (<see cref="CheckSuccess"/>) и
  /// завершить розыгрыш (<see cref="UseCard"/>). Так неверный розыгрыш отклоняется, ничего не меняя.
  /// Для сетевой игры и ботов; интерфейс с анимацией кубика вызывает <see cref="CheckSuccess"/> и <see cref="UseCard"/> сам.
  /// </summary>
  /// <param name="cell">Ячейка карточки.</param>
  /// <param name="option">Вариант действия карточки.</param>
  /// <param name="context">Контекст с выбранными целями.</param>
  /// <returns>Проверка успеха.</returns>
  /// <exception cref="InvalidOperationException">Карточку так сыграть нельзя; состояние партии не изменилось.</exception>
  public SuccessCheck PlayCard(Cell cell, Effect option, GameContext context)
  {
    RequireAction();
    if (context.CurrentPlayer != CurrentPlayer)
      throw new InvalidOperationException("Карточку играет не тот игрок, который ходит.");
    var check = Field.CheckUse(cell, option, context);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    CheckSuccess(cell);
    return UseCard(option, context);
  }

  /// <summary>
  /// Завершить розыгрыш карточки после проверки успеха (<see cref="CheckSuccess"/>). Это действие.
  /// Если проверка успешна, выполняется выбранный вариант действия карточки (<see cref="PlayingField.Use"/>),
  /// иначе действие не выполняется (<see cref="PlayingField.Waste"/>). В обоих случаях карточка переворачивается
  /// (после быстрого действия — поворачивается). Быстрое действие (<see cref="Effect.IsQuick"/>) не тратит действие хода.
  /// </summary>
  /// <param name="option">Выбранный вариант действия карточки.</param>
  /// <param name="context">Контекст партии.</param>
  /// <returns>Проверка успеха.</returns>
  /// <exception cref="InvalidOperationException">Проверка успеха не проводилась.</exception>
  public SuccessCheck UseCard(Effect option, GameContext context)
  {
    var check = PendingCheck ?? throw new InvalidOperationException("Сначала нужно провести проверку успеха.");
    SetPendingCheck(null);
    RequireAction();
    var player = CurrentPlayer;
    var money = player.Money;
    var played = Field.CardAt(check.Cell);
    var targets = TargetsOf(context);
    var source = played?.Name ?? "Действие";
    // Бросок уже сделан: даже если действие откажет, карточка сыграна (см. PlayingField.Use), и ход тратится как обычно.
    try
    {
      using (CurrentPlayer.EarningFrom(source))
      {
        if (check.Succeeded)
          Field.Use(check.Cell, option, context);
        else
          Field.Waste(check.Cell, CurrentPlayer, option.IsQuick, check.Die);
      }
    }
    finally
    {
      // Быстрое действие не тратит действие игрока, пока карточка только поворачивается. Если же она перевернулась
      // рубашкой вверх (второе быстрое действие), действие игрока тратится.
      // Карточку, ушедшую из игры, больше не сыграть — действие тоже тратится.
      if (!option.IsQuick || Field.IsUsed(check.Cell) || Field.CardAt(check.Cell) is null)
        CompleteAction();
    }

    var effect = context.BorrowedOption is { } borrowed ? $"{option.Description}: {borrowed.Description}" : option.Description;
    Report(new PlayerAction(player, PlayerActionKind.PlayCard, money, played, check.Cell, null, effect, targets, check));
    return check;
  }

  /// <summary>
  /// Игрок, который сейчас ходит, применяет свою карточку теории (<see cref="PlayingField.UseTheoryCard"/>).
  /// Это действие. Карточка переворачивается и восстанавливается в фазе обновления карточек.
  /// </summary>
  /// <param name="card">Карточка теории.</param>
  /// <param name="context">Контекст партии с выбранными целями (например, теорией).</param>
  public void UseTheoryCard(OwnedTheoryCard card, GameContext context)
  {
    RequireAction();
    if (context.CurrentPlayer != CurrentPlayer)
      throw new InvalidOperationException("Карточку теории применяет не тот игрок, который ходит.");
    // Сначала проверяем все цели: неверное применение отклоняется, ничего не меняя.
    var precheck = Field.CheckTheoryCardUse(card, context);
    if (!precheck.IsAllowed)
      throw new InvalidOperationException(precheck.Reason);
    var player = CurrentPlayer;
    var money = player.Money;
    var targets = TargetsOf(context, except: Field.ColumnOf(card.Card.Theory));
    var home = Field.ColumnOf(card.Card.Theory) is { } column ? RefOf(column) : null;

    TheoryTrigger? trigger;
    try
    {
      trigger = Field.UseTheoryCard(card, context);
    }
    catch
    {
      // Действие отказало, но карточка уже применена (часть действия могла выполниться): ход тратится.
      if (card.IsUsed)
        CompleteAction();
      throw;
    }

    if (trigger is not null)
      SetPendingTheoryTrigger(trigger);
    else
      CompleteAction();
    Report(new PlayerAction(player, PlayerActionKind.UseTheoryCard, money, card.Card, card.Cell, home,
      card.Card.OnPlayFromHand.Description, targets));
  }

  /// <summary>
  /// Действие основной теории, которое ждёт выбора владельца основной теории (после <see cref="UseTheoryCard"/>), или <c>null</c>.
  /// Пока оно не разрешено, другие действия сделать нельзя; ход не заканчивается.
  /// </summary>
  public TheoryTrigger? PendingTheoryTrigger { get; private set; }

  /// <summary>
  /// Начался или завершился выбор для действия основной теории; аргумент — <see cref="PendingTheoryTrigger"/>.
  /// </summary>
  public event Action<TheoryTrigger?>? PendingTheoryTriggerChanged;

  /// <summary>
  /// Владелец основной теории выбирает карточку в руке для её действия.
  /// </summary>
  /// <exception cref="InvalidOperationException">Выбора нет или карточку выбрать нельзя.</exception>
  public void SelectTriggerHandCard(Card card)
  {
    var trigger = RequirePendingTheoryTrigger();
    var effect = trigger.Effect;
    if (!(effect.RequiresHandCard || effect.AcceptsHandCardTarget) || !trigger.Owner.Hand.Contains(card))
      throw new InvalidOperationException("Эту карточку выбрать нельзя.");
    if (!effect.RequiresHandCard && effect.CheckHandCard(card, trigger.Context) is { IsAllowed: false } denied)
      throw new InvalidOperationException(denied.Reason);
    trigger.HandCard = card;
    var money = trigger.Owner.Money;
    ContinueTheoryTrigger(trigger);
    Report(new PlayerAction(trigger.Owner, PlayerActionKind.TriggerHandCard, money, card, Effect: effect.Description,
      Targets: new ActionTargets(HandCard: card)));
  }

  /// <summary>
  /// Владелец основной теории выбирает теорию для её действия.
  /// </summary>
  /// <exception cref="InvalidOperationException">Выбора нет или теорию выбрать нельзя.</exception>
  public void SelectTriggerTheory(TheoryColumn theory)
  {
    var trigger = RequirePendingTheoryTrigger();
    if (!trigger.Effect.RequiresTheory || !trigger.Effect.CanTargetTheory(theory, trigger.Owner))
      throw new InvalidOperationException($"Теорию «{theory}» выбрать нельзя.");
    trigger.Theory = theory;
    var money = trigger.Owner.Money;
    var chosen = RefOf(theory);
    ContinueTheoryTrigger(trigger);
    Report(new PlayerAction(trigger.Owner, PlayerActionKind.TriggerTheory, money, Theory: chosen, Effect: trigger.Effect.Description));
  }

  /// <summary>
  /// Владелец основной теории выбирает карточку на поле для её действия.
  /// </summary>
  /// <exception cref="InvalidOperationException">Выбора нет или карточку выбрать нельзя.</exception>
  public void SelectTriggerCell(Cell cell)
  {
    var trigger = RequirePendingTheoryTrigger();
    if (!trigger.Effect.RequiresTargetCell)
      throw new InvalidOperationException("Здесь нужен выбор не карточки на поле.");
    var check = trigger.Effect.CheckTargetCell(cell, trigger.Context);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);
    trigger.Cell = cell;
    var money = trigger.Owner.Money;
    var target = RefAt(cell);
    ContinueTheoryTrigger(trigger);
    Report(new PlayerAction(trigger.Owner, PlayerActionKind.TriggerCell, money, target?.Card, cell, Effect: trigger.Effect.Description,
      Targets: new ActionTargets(Target: target)));
  }

  /// <summary>
  /// Владелец основной теории отказывается от её действия: оно не выполняется, действие хода тратится.
  /// </summary>
  public void SkipTheoryTrigger()
  {
    var trigger = RequirePendingTheoryTrigger();
    SetPendingTheoryTrigger(null);
    CompleteAction();
    Report(new PlayerAction(trigger.Owner, PlayerActionKind.SkipTrigger, trigger.Owner.Money, Effect: trigger.Effect.Description));
  }

  private TheoryTrigger RequirePendingTheoryTrigger() =>
    PendingTheoryTrigger ?? throw new InvalidOperationException("Сейчас ничего не выбирают.");

  /// <summary>
  /// Все выборы сделаны — действие выполняется, и тратится действие хода игрока, применившего дочернюю карточку.
  /// Иначе ждём остальных выборов.
  /// </summary>
  private void ContinueTheoryTrigger(TheoryTrigger trigger)
  {
    if (!trigger.IsComplete)
    {
      PendingTheoryTriggerChanged?.Invoke(trigger);
      return;
    }

    SetPendingTheoryTrigger(null);
    try
    {
      Field.ResolveTheoryTrigger(trigger);
    }
    catch (Exception)
    {
      SetPendingTheoryTrigger(trigger);
      throw;
    }
    CompleteAction();
  }

  private void SetPendingTheoryTrigger(TheoryTrigger? trigger)
  {
    PendingTheoryTrigger = trigger;
    PendingTheoryTriggerChanged?.Invoke(trigger);
  }

  /// <summary>
  /// Передать ход следующему игроку, который ещё не спасовал.
  /// </summary>
  public void EndTurn()
  {
    RequireNoPendingCheck();
    RequirePhase(GamePhase.PlayerTurns);
    Turns.EndTurn();
    AutoPass();
  }

  /// <summary>
  /// Игрок, который сейчас ходит, пасует и до конца раунда больше не ходит. Когда спасуют все,
  /// начинается проверка штабов теорий.
  /// </summary>
  public void Pass()
  {
    RequireNoPendingCheck();
    RequirePhase(GamePhase.PlayerTurns);
    var player = CurrentPlayer;
    var money = player.Money;
    // Мастера теорий приводят адептов после паса, пока игрок ещё текущий.
    Field.AddMasterAdepts(CurrentPlayer);
    PassCurrent();
    Report(new PlayerAction(player, PlayerActionKind.Pass, money));
    AutoPass();
  }

  /// <summary>
  /// Может ли игрок ещё что-то сделать: у него есть карточки в руке, несыгранные карточки в личном штабе,
  /// карточки теорий, которые ещё можно применить, или на рынке есть карточки.
  /// </summary>
  public bool HasMoves(Player player) =>
    player.Hand.Cards.Count > 0 || Field.HasUnusedCards(player) || Market.OpenCards.Count > 0
    || Field.TheoryCards.Any(card => Field.CanUseTheoryCard(card, player));

  /// <summary>
  /// Проверка штабов теорий: закрыть несостоятельные (<see cref="TheoryColumn.IsInsolvent"/>) — те, где больше
  /// <see cref="TheoryColumn.SolvencyCheckLeadersThreshold"/> лидеров и бездомных больше, чем остальных персонажей.
  /// После этого адепты действующих теорий приносят деньги.
  /// </summary>
  /// <returns>Штабы, закрытые сейчас.</returns>
  public SolvencyChanges CloseInsolventTheories()
  {
    _incomeOfRound.Clear();
    RequirePhase(GamePhase.HeadquartersCheck);
    var closed = Field.CloseInsolventTheories();
    var reopened = Field.ReopenSolventTheories();
    SolvencyOfRound = new SolvencyChanges(closed, reopened);

    // Пассивные способности карточек теорий срабатывают в конце раунда, уже после закрытия штабов.
    foreach (var owned in Field.TheoryCards.ToList())
    {
      var earned = 0;
      void OnEarned(Player _, int amount) => earned += amount;

      owned.Owner.Earned += OnEarned;
      try
      {
        using (owned.Owner.EarningFrom($"Пассивно: «{owned.Card.Name}»"))
          owned.Card.HandleEvent(new RoundEnded(), new GameContext(owned.Owner, Field, _random, Market));
      }
      finally
      {
        owned.Owner.Earned -= OnEarned;
      }
      Field.ReportTheoryEarned(owned, earned);
    }

    EnqueueIncomeTheories();
    SetPhase(GamePhase.AdeptIncome);
    return new SolvencyChanges(closed, reopened);
  }

  /// <summary>
  /// Доход от адептов за конец раунда: теории, которые принесли деньги, в порядке выплаты. Заполняется при выплатах
  /// (<see cref="PayNextTheoryIncome"/>) и показывается в начале следующего раунда.
  /// </summary>
  public IReadOnlyList<TheoryIncome> IncomeOfRound => _incomeOfRound;

  /// <summary>
  /// Какие штабы теорий закрылись и открылись при последней проверке штабов (в конце раунда).
  /// </summary>
  public SolvencyChanges SolvencyOfRound { get; private set; } = new(Array.Empty<TheoryColumn>(), Array.Empty<TheoryColumn>());

  /// <summary>
  /// Адепты следующей действующей теории приносят деньги её лидерам (<see cref="TheoryIncome"/>).
  /// Когда все теории получили доход, в обычном раунде начинается обновление карточек, в итоговом подсчёте партия завершается.
  /// Миплов-лидеров (<see cref="TheoryColumn.LeaderMeeples"/>) снимают при обновлении рынка (<see cref="RefreshMarket"/>),
  /// только если будет следующий раунд: в итоговом подсчёте они остаются в теориях.
  /// </summary>
  /// <returns>Доход теории или <c>null</c>, если все теории уже получили доход.</returns>
  public TheoryIncome? PayNextTheoryIncome()
  {
    if (Phase is not (GamePhase.AdeptIncome or GamePhase.FinalAdeptIncome))
      throw new InvalidOperationException($"Доход от адептов нельзя получить в фазе {Phase}.");
    if (!_unpaidTheories.TryDequeue(out var column))
    {
      SetPhase(Phase == GamePhase.AdeptIncome ? GamePhase.CardsRefresh : GamePhase.GameOver);
      return null;
    }

    // Закрытая теория приносит доход только владельцу «Иллюминатов», если он в ней лидер.
    var income = TheoryIncome.Of(column);
    income = Field.IlluminatiBeneficiary(column) is { } beneficiary ? income.OnlyFor(beneficiary) : income;
    foreach (var (player, amount) in income.ByPlayer)
    {
      using (player.EarningFrom($"Адепты: «{column.Theory}»"))
        player.Earn(amount);
    }
    if (income.Total > 0)
      _incomeOfRound.Add(income);
    return income;
  }

  /// <summary>
  /// Обновление карточек: сыгранные карточки личных штабов переворачиваются лицевой стороной вверх.
  /// После этого обновляется рынок.
  /// </summary>
  public void RefreshPlayedCards()
  {
    RequirePhase(GamePhase.CardsRefresh);
    Field.RefreshUsedCards();
    SetPhase(GamePhase.MarketRefresh);
  }

  /// <summary>
  /// Обновить рынок: нижняя строка уходит, верхняя сдвигается вниз, пустые места заполняются из общей колоды.
  /// Если в колоде не осталось карточек для выкладывания, рынок не обновляется — начинается итоговый подсчёт.
  /// </summary>
  /// <returns><c>true</c>, если рынок обновлён и будет следующий раунд; <c>false</c>, если начался итоговый подсчёт.</returns>
  public bool RefreshMarket()
  {
    RequirePhase(GamePhase.MarketRefresh);
    // В режиме гонки партия заканчивается после раунда, в котором теория дошла до финиша.
    if (Market.DeckCount == 0 || Mode == GameMode.Race && IsLastRound)
    {
      // Перед итоговым подсчётом все закрытые карточки раскрываются: их владельцы получают бонусы или карточки уходят.
      // Либо владельцы по очереди сами решают, кого раскрыть.
      if (Field.RevealHiddenAtEnd)
        Field.RevealAllHiddenCards();
      else
      {
        _finalRevealQueue.Clear();
        var first = Turns.FirstPlayer;
        for (var player = first; ; )
        {
          if (Field.OwnHiddenCells(player).Any())
            _finalRevealQueue.Enqueue(player);
          player = Turns.NextAfter(player);
          if (player == first)
            break;
        }
        if (_finalRevealQueue.Count > 0)
        {
          NextFinalRevealPlayer();
          SetPhase(GamePhase.FinalReveal);
          return false;
        }
      }
      StartFinalScoring();
      return false;
    }

    // Будет следующий раунд: миплы-лидеры, поставленные в этом раунде, снимаются.
    Field.RemoveLeaderMeeples();
    Market.Refresh();
    return true;
  }

  private readonly Queue<Player> _finalRevealQueue = new();

  /// <summary>
  /// Кто сейчас выбирает, каких своих закрытых персонажей раскрыть (<see cref="GamePhase.FinalReveal"/>), или <c>null</c>.
  /// Он же — текущий игрок (<see cref="CurrentPlayer"/>).
  /// </summary>
  public Player? FinalRevealPlayer { get; private set; }

  /// <summary>
  /// Закрытые персонажи, которых <see cref="FinalRevealPlayer"/> ещё может раскрыть.
  /// </summary>
  public IEnumerable<Cell> FinalRevealTargets() =>
    Phase == GamePhase.FinalReveal && FinalRevealPlayer is { } player ? Field.OwnHiddenCells(player) : [];

  /// <summary>
  /// <see cref="FinalRevealPlayer"/> раскрывает своего закрытого персонажа.
  /// </summary>
  /// <exception cref="InvalidOperationException">Сейчас не конец игры или это не его закрытый персонаж.</exception>
  public void RevealAtEnd(Cell cell)
  {
    RequirePhase(GamePhase.FinalReveal);
    Field.RevealOwn(cell, FinalRevealPlayer!);
  }

  /// <summary>
  /// <see cref="FinalRevealPlayer"/> больше не раскрывает закрытых персонажей: выбирает следующий владелец закрытых
  /// персонажей, а когда выбрали все — начинается итоговый подсчёт.
  /// </summary>
  public void FinishFinalReveal()
  {
    RequirePhase(GamePhase.FinalReveal);
    if (!NextFinalRevealPlayer())
      StartFinalScoring();
  }

  /// <summary>
  /// Следующий владелец закрытых персонажей становится текущим игроком. <c>false</c> — выбрали все.
  /// </summary>
  private bool NextFinalRevealPlayer()
  {
    // Пока шла очередь, чужие раскрытия могли убрать его закрытых персонажей — такого пропускаем.
    while (_finalRevealQueue.TryDequeue(out var player))
    {
      if (!Field.OwnHiddenCells(player).Any())
        continue;
      FinalRevealPlayer = player;
      Turns.SetCurrent(player);
      return true;
    }
    FinalRevealPlayer = null;
    return false;
  }

  /// <summary>
  /// Закрытые карточки раскрыты (или их владельцы решили, кого раскрыть): штабы проверяются, и начинается итоговый
  /// подсчёт — или партия сразу завершается, если в этом режиме выплат в конце нет.
  /// </summary>
  private void StartFinalScoring()
  {
    // Раскрытие могло сделать штаб несостоятельным (или наоборот): проверяем штабы, как в конце раунда.
    Field.CloseInsolventTheories();
    Field.ReopenSolventTheories();
    // В режиме развития теорий денег за лидеров и повторного дохода от адептов нет: очки считаются по состоянию поля.
    if (Mode is GameMode.TheoryDevelopment or GameMode.Race)
    {
      SetPhase(GamePhase.GameOver);
      return;
    }
    EnqueueActiveTheories(column => true);
    SetPhase(GamePhase.FinalLeadersBonus);
  }

  /// <summary>
  /// Итоговый подсчёт: владелец следующей действующей теории получает по <see cref="LeadersBonus.PerLeader"/>$
  /// за каждого её лидера. Когда награды выданы, адепты ещё раз приносят деньги (<see cref="PayNextTheoryIncome"/>).
  /// </summary>
  /// <returns>Награда или <c>null</c>, если все владельцы её уже получили.</returns>
  public LeadersBonus? PayNextLeadersBonus()
  {
    RequirePhase(GamePhase.FinalLeadersBonus);
    if (!_unpaidTheories.TryDequeue(out var column))
    {
      EnqueueIncomeTheories();
      SetPhase(GamePhase.FinalAdeptIncome);
      return null;
    }

    var bonus = LeadersBonus.Of(column);
    using (bonus.Owner.EarningFrom($"Лидеры: «{column.Theory}»"))
      bonus.Owner.Earn(bonus.Amount);
    return bonus;
  }

  /// <summary>
  /// Итоги партии: в режиме денег — от самого богатого к самому бедному; в режимах развития теорий и гонки — по победным очкам
  /// (при равенстве — по деньгам). При полном равенстве — в порядке хода.
  /// </summary>
  public IReadOnlyList<Player> Ranking => Mode switch
  {
    GameMode.Money => Players.OrderByDescending(player => player.Money).ToList(),
    GameMode.Race => Players.OrderByDescending(player => RaceScoreOf(player).Total).ThenByDescending(player => player.Money).ToList(),
    _ => Players.OrderByDescending(player => ScoreOf(player).Total).ThenByDescending(player => player.Money).ToList(),
  };

  /// <summary>
  /// Победители: первые в <see cref="Ranking"/> и все, кто с ними поровну (по очкам и деньгам или по деньгам в режиме денег).
  /// </summary>
  public IReadOnlyList<Player> Winners
  {
    get
    {
      var ranking = Ranking;
      var best = RankKey(ranking[0]);
      return ranking.TakeWhile(player => RankKey(player) == best).ToList();
    }
  }

  private (int Points, int Money) RankKey(Player player) => Mode switch
  {
    GameMode.Money => (0, player.Money),
    GameMode.Race => (RaceScoreOf(player).Total, player.Money),
    _ => (ScoreOf(player).Total, player.Money),
  };

  /// <summary>
  /// Победные очки игрока в режиме гонки (<see cref="RaceScore"/>) по текущему состоянию поля.
  /// </summary>
  public RaceScore RaceScoreOf(Player player) => RaceScoring.Of(player, Field, Players.Count);

  /// <summary>
  /// Победные очки игрока (режим развития теорий, <see cref="VictoryScore"/>) по текущему состоянию поля.
  /// </summary>
  public VictoryScore ScoreOf(Player player) => VictoryPoints.Of(player, Field);

  /// <summary>
  /// Начать следующий раунд: пасы сброшены, первым ходит игрок с жетоном первого игрока. В первом раунде
  /// он выбирается случайно, затем в начале каждого следующего раунда жетон переходит по кругу направо.
  /// </summary>
  public void StartNextRound()
  {
    if (Phase is not (GamePhase.Setup or GamePhase.MarketRefresh))
      throw new InvalidOperationException($"Новый раунд нельзя начать в фазе {Phase}.");

    var first = Round == 0 ? Players[_random.Next(Players.Count)] : Turns.NextAfter(Turns.FirstPlayer);
    Round++;
    SetPhase(GamePhase.PlayerTurns);
    Turns.StartRound(first);
    AutoPass();
  }

  /// <summary>
  /// Контекст для действий игрока, который сейчас ходит.
  /// </summary>
  public GameContext CreateContext() => new(CurrentPlayer, Field, _random, Market);

  /// <summary>
  /// Может ли игрок, который ходит, сейчас сделать действие хода: идут ходы игроков, ничего не ждёт выбора
  /// и действия ещё остались. Ничего не меняет.
  /// </summary>
  public PlacementCheck CheckAction()
  {
    var pending = CheckNoPending();
    if (!pending.IsAllowed)
      return pending;
    if (Phase != GamePhase.PlayerTurns)
      return PlacementCheck.Deny($"Это можно сделать только в фазе {GamePhase.PlayerTurns}, а сейчас {Phase}.");
    return ActionsLeft == 0
      ? PlacementCheck.Deny($"У игрока {CurrentPlayer} не осталось действий в этом ходу.")
      : PlacementCheck.Allowed;
  }

  /// <summary>
  /// Ничего не ждёт завершения: ни проверка успеха, ни выбор цели выложенной карточки, ни выбор для действия
  /// основной теории. Ничего не меняет.
  /// </summary>
  public PlacementCheck CheckNoPending()
  {
    if (PendingCheck is not null)
      return PlacementCheck.Deny("Идёт проверка успеха карточки — дождитесь её окончания.");
    if (PendingPlacement is not null)
      return PlacementCheck.Deny("Сначала выберите цель для действия выложенной карточки.");
    if (PendingTheoryTrigger is not null)
      return PlacementCheck.Deny("Владелец основной теории ещё выбирает цель её действия.");
    return PlacementCheck.Allowed;
  }

  private void RequireAction()
  {
    var check = CheckAction();
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);
  }

  /// <summary>
  /// Действие сделано: если действий больше не осталось, ход переходит к следующему игроку.
  /// </summary>
  private void CompleteAction()
  {
    SetActionsLeft(ActionsLeft - 1);
    if (ActionsLeft == 0)
      Turns.EndTurn();
    AutoPass();
  }

  /// <summary>
  /// Пока игроку, который сейчас ходит, нечем ходить, он пасует автоматически.
  /// </summary>
  /// <remarks>
  /// Вызывается после смены хода, а не из события смены хода: иначе подписчики получили бы события
  /// о ходах вперемешку.
  /// </remarks>
  private void AutoPass()
  {
    CheckRaceFinish();
    while (Phase == GamePhase.PlayerTurns && !HasMoves(CurrentPlayer))
    {
      AutoPassed?.Invoke(CurrentPlayer);
      PassCurrent();
    }
  }

  private void PassCurrent()
  {
    var passing = CurrentPlayer;
    Turns.Pass();
    // Персонажи со значком руля возвращаются к игроку, который спасовал.
    Field.ReturnWheeledCards(passing);
    if (Turns.AllPassed)
      SetPhase(GamePhase.HeadquartersCheck);
  }

  private void RequireNoPendingCheck()
  {
    var check = CheckNoPending();
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);
  }

  private void SetPendingCheck(SuccessCheck? check)
  {
    // Быстрое действие не завершает действие хода — проверяем финиш, когда проверка успеха закончилась.
    if (check is null)
      CheckRaceFinish();
    PendingCheck = check;
    PendingCheckChanged?.Invoke(check);
  }

  private void SetActionsLeft(int actionsLeft)
  {
    ActionsLeft = actionsLeft;
    ActionsLeftChanged?.Invoke(actionsLeft);
  }

  private void RequirePhase(GamePhase phase)
  {
    if (Phase != phase)
      throw new InvalidOperationException($"Это можно сделать только в фазе {phase}, а сейчас {Phase}.");
  }

  /// <summary>
  /// Поставить в очередь на выплату действующие (не закрытые) теории, подходящие под условие.
  /// </summary>
  private void EnqueueActiveTheories(Func<TheoryColumn, bool> predicate)
  {
    _unpaidTheories.Clear();
    // Слева направо: деньги за теории выплачиваются от левого края поля к правому.
    foreach (var column in Field.TheoryColumns.Where(column => !column.IsClosed && predicate(column)).OrderBy(column => column.TheoryCell.Column))
      _unpaidTheories.Enqueue(column);
  }

  /// <summary>
  /// Поставить в очередь теории, которые приносят доход от адептов: открытые с адептами и закрытые,
  /// где есть владелец «Иллюминатов» в роли лидера.
  /// </summary>
  private void EnqueueIncomeTheories()
  {
    _unpaidTheories.Clear();
    foreach (var column in Field.TheoryColumns
      .Where(column => column.Adepts > 0 && (!column.IsClosed || Field.IlluminatiBeneficiary(column) is not null))
      .OrderBy(column => column.TheoryCell.Column))
      _unpaidTheories.Enqueue(column);
  }

  /// <summary>
  /// Режим гонки: если какая-то теория дошла до финиша, текущий раунд объявляется последним.
  /// </summary>
  private void CheckRaceFinish()
  {
    if (Mode != GameMode.Race || IsLastRound || Phase is GamePhase.Setup or GamePhase.GameOver)
      return;
    if (Field.TheoryColumns.FirstOrDefault(column => RaceScoring.IsFinished(column, Players.Count)) is not { } finished)
      return;

    IsLastRound = true;
    LastRoundDeclared?.Invoke(finished);
  }

  /// <summary>
  /// Клиент: партия восстановлена по присланному сервером виду (<see cref="Client.ClientMirror"/>) — всё на столе
  /// могло измениться, интерфейс перерисовывает его целиком.
  /// </summary>
  public event Action? Restored;

  /// <summary>
  /// Клиент: восстановить фазу, раунд и незавершённые выборы по присланному сервером виду. Событий не вызывает.
  /// </summary>
  internal void Restore(GamePhase phase, int round, bool isLastRound, int actionsLeft, SuccessCheck? check, PlacementChoice? placement, TheoryTrigger? trigger)
  {
    Phase = phase;
    Round = round;
    IsLastRound = isLastRound;
    ActionsLeft = actionsLeft;
    PendingCheck = check;
    PendingPlacement = placement;
    PendingTheoryTrigger = trigger;
    _unpaidTheories.Clear();
  }

  internal void RaiseRestored() => Restored?.Invoke();

  private void SetPhase(GamePhase phase)
  {
    CheckRaceFinish();
    Phase = phase;
    PhaseChanged?.Invoke(phase);
  }
}
