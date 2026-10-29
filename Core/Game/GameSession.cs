using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Decks;
using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Field;

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
  private readonly Random _random;

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
  /// Подготовить партию: поле стандартного размера, личные штабы игроков стоят в ряд внизу поля,
  /// перемешанная общая колода (<see cref="MarketDeck"/>) лежит на рынке.
  /// </summary>
  /// <param name="players">Игроки в порядке хода.</param>
  /// <param name="random">Источник случайности; по умолчанию — общий.</param>
  public GameSession(IEnumerable<Player> players, Random? random = null)
  {
    Turns = new TurnOrder(players);
    Field = new PlayingField(FieldLayout.Columns, FieldLayout.Rows, FieldLayout.CommonZone(Players.Count));
    for (var i = 0; i < Players.Count; i++)
      Field.AddPlayer(Players[i], FieldLayout.PlayerZone(i, Players.Count));
    _random = random ?? Random.Shared;
    Market = new Market(FieldLayout.MarketZone, MarketDeck.Create(Players.Count, _random));
    // Подписываемся первыми: к моменту, когда о новом ходе узнают остальные, действия уже восстановлены.
    Turns.TurnChanged += _ => SetActionsLeft(ActionsPerTurn);
  }

  /// <summary>
  /// Подготовка и начало первого раунда: каждый игрок получает стартовую руку (<see cref="StartingHand"/>) —
  /// новых бездомных и граждан со случайными теориями, на рынке открывается по карточке на игрока в первую и третью строки.
  /// </summary>
  public void Start()
  {
    RequirePhase(GamePhase.Setup);
    foreach (var player in Players)
    {
      foreach (var card in StartingHand.Create(_random))
        player.Hand.Add(card);
    }

    Market.OpenStartingCards(Players.Count);
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
    var card = Market.Buy(cell, CurrentPlayer);
    CompleteAction();
    return card;
  }

  /// <summary>
  /// Игрок, который сейчас ходит, выкладывает карточку с руки на поле — в личный штаб или в общую зону. Это действие.
  /// </summary>
  /// <returns>Условия, на которых карточка положена.</returns>
  /// <exception cref="InvalidOperationException">Карточки нет в руке или её сюда класть нельзя.</exception>
  public PlacementCheck PlaceFromHand(Card card, Cell cell)
  {
    RequireAction();
    var hand = CurrentPlayer.Hand;
    if (!hand.Contains(card))
      throw new InvalidOperationException($"Карточки «{card}» нет в руке игрока {CurrentPlayer}.");

    var check = Field.Place(card, cell, CurrentPlayer);
    hand.Remove(card);
    CompleteAction();
    return check;
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
  /// Завершить розыгрыш карточки после проверки успеха (<see cref="CheckSuccess"/>). Это действие.
  /// Если проверка успешна, выполняется выбранный вариант действия карточки (<see cref="PlayingField.Use"/>),
  /// иначе действие не выполняется (<see cref="PlayingField.Waste"/>). В обоих случаях карточка переворачивается.
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
    if (check.Succeeded)
      Field.Use(check.Cell, option, context);
    else
      Field.Waste(check.Cell, CurrentPlayer);
    CompleteAction();
    return check;
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
    PassCurrent();
    AutoPass();
  }

  /// <summary>
  /// Может ли игрок ещё что-то сделать: у него есть карточки в руке, несыгранные карточки в личном штабе
  /// или на рынке есть карточки.
  /// </summary>
  public bool HasMoves(Player player) =>
    player.Hand.Cards.Count > 0 || Field.HasUnusedCards(player) || Market.OpenCards.Count > 0;

  /// <summary>
  /// Проверка штабов теорий: закрыть несостоятельные (<see cref="TheoryColumn.IsInsolvent"/>) — те, где больше
  /// <see cref="TheoryColumn.SolvencyCheckLeadersThreshold"/> лидеров и бездомных больше, чем остальных персонажей.
  /// После этого адепты действующих теорий приносят деньги.
  /// </summary>
  /// <returns>Штабы, закрытые сейчас.</returns>
  public IReadOnlyList<TheoryColumn> CloseInsolventTheories()
  {
    RequirePhase(GamePhase.HeadquartersCheck);
    var closed = Field.CloseInsolventTheories();

    _unpaidTheories.Clear();
    foreach (var column in Field.TheoryColumns.Where(column => !column.IsClosed && column.Adepts > 0))
      _unpaidTheories.Enqueue(column);
    SetPhase(GamePhase.AdeptIncome);
    return closed;
  }

  /// <summary>
  /// Адепты следующей действующей теории приносят деньги её лидерам (<see cref="TheoryIncome"/>).
  /// Когда все теории получили доход, начинается обновление карточек.
  /// </summary>
  /// <returns>Доход теории или <c>null</c>, если все теории уже получили доход.</returns>
  public TheoryIncome? PayNextTheoryIncome()
  {
    RequirePhase(GamePhase.AdeptIncome);
    if (!_unpaidTheories.TryDequeue(out var column))
    {
      SetPhase(GamePhase.CardsRefresh);
      return null;
    }

    var income = TheoryIncome.Of(column);
    foreach (var (player, amount) in income.ByPlayer)
      player.Earn(amount);
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
  /// </summary>
  public void RefreshMarket()
  {
    RequirePhase(GamePhase.MarketRefresh);
    Market.Refresh();
  }

  /// <summary>
  /// Начать следующий раунд: пасы сброшены, первым ходит первый игрок.
  /// </summary>
  public void StartNextRound()
  {
    if (Phase is not (GamePhase.Setup or GamePhase.MarketRefresh))
      throw new InvalidOperationException($"Новый раунд нельзя начать в фазе {Phase}.");

    Round++;
    SetPhase(GamePhase.PlayerTurns);
    Turns.StartRound();
    AutoPass();
  }

  /// <summary>
  /// Контекст для действий игрока, который сейчас ходит.
  /// </summary>
  public GameContext CreateContext() => new(CurrentPlayer, Field);

  private void RequireAction()
  {
    RequireNoPendingCheck();
    RequirePhase(GamePhase.PlayerTurns);
    if (ActionsLeft == 0)
      throw new InvalidOperationException($"У игрока {CurrentPlayer} не осталось действий в этом ходу.");
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
    while (Phase == GamePhase.PlayerTurns && !HasMoves(CurrentPlayer))
    {
      AutoPassed?.Invoke(CurrentPlayer);
      PassCurrent();
    }
  }

  private void PassCurrent()
  {
    Turns.Pass();
    if (Turns.AllPassed)
      SetPhase(GamePhase.HeadquartersCheck);
  }

  private void RequireNoPendingCheck()
  {
    if (PendingCheck is not null)
      throw new InvalidOperationException("Идёт проверка успеха карточки — дождитесь её окончания.");
  }

  private void SetPendingCheck(SuccessCheck? check)
  {
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

  private void SetPhase(GamePhase phase)
  {
    Phase = phase;
    PhaseChanged?.Invoke(phase);
  }
}
