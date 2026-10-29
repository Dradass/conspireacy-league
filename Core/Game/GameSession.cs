using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Decks;
using ConspiracyLeague.Core.Field;

namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Сессия игры — всё состояние партии: игроки и их балансы, очерёдность ходов, поле с личными штабами
/// и выложенные теории (какие карточки в каждой теории, кто их владельцы, сколько адептов).
/// </summary>
/// <remarks>
/// Партия идёт по фазам (<see cref="GamePhase"/>): подготовка (<see cref="Start"/>), затем раунды.
/// Раунд: игроки ходят, пока все не спасуют (<see cref="Pass"/>); затем закрываются несостоятельные штабы
/// (<see cref="CloseInsolventTheories"/>), адепты приносят деньги (<see cref="PayNextTheoryIncome"/>),
/// обновляется рынок (<see cref="RefreshMarket"/>), и начинается следующий раунд (<see cref="StartNextRound"/>).
/// Шаги конца раунда вызываются по одному, чтобы между ними можно было показать, что произошло.
/// </remarks>
public sealed class GameSession
{
  /// <summary>
  /// Сколько карточек из общей колоды получает каждый игрок при подготовке.
  /// </summary>
  public const int StartingHandSize = 3;

  private readonly Queue<TheoryColumn> _unpaidTheories = new();

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
    Field = new PlayingField(FieldLayout.Columns, FieldLayout.Rows, FieldLayout.CommonZone);
    for (var i = 0; i < Players.Count; i++)
      Field.AddPlayer(Players[i], FieldLayout.PlayerZone(i, Players.Count));
    Market = new Market(FieldLayout.MarketZone, MarketDeck.Create(Players.Count, random ?? Random.Shared));
  }

  /// <summary>
  /// Подготовка и начало первого раунда: каждый игрок получает <see cref="StartingHandSize"/> случайных карточек
  /// из общей колоды, на рынке открывается по карточке на игрока в первую и третью строки.
  /// </summary>
  public void Start()
  {
    RequirePhase(GamePhase.Setup);
    for (var i = 0; i < StartingHandSize; i++)
    {
      foreach (var player in Players)
        player.Hand.Add(Market.Draw() ?? throw new InvalidOperationException("Общая колода закончилась при раздаче."));
    }

    Market.OpenStartingCards(Players.Count);
    StartNextRound();
  }

  /// <summary>
  /// Игрок, который сейчас ходит, покупает карточку с рынка в руку.
  /// </summary>
  /// <param name="cell">Ячейка рынка.</param>
  /// <returns>Купленная карточка.</returns>
  public PersonCard BuyFromMarket(Cell cell)
  {
    RequirePhase(GamePhase.PlayerTurns);
    return Market.Buy(cell, CurrentPlayer);
  }

  /// <summary>
  /// Игрок, который сейчас ходит, выкладывает карточку с руки на поле — в личный штаб или в общую зону.
  /// </summary>
  /// <returns>Условия, на которых карточка положена.</returns>
  /// <exception cref="InvalidOperationException">Карточки нет в руке или её сюда класть нельзя.</exception>
  public PlacementCheck PlaceFromHand(Card card, Cell cell)
  {
    RequirePhase(GamePhase.PlayerTurns);
    var hand = CurrentPlayer.Hand;
    if (!hand.Contains(card))
      throw new InvalidOperationException($"Карточки «{card}» нет в руке игрока {CurrentPlayer}.");

    var check = Field.Place(card, cell, CurrentPlayer);
    hand.Remove(card);
    return check;
  }

  /// <summary>
  /// Передать ход следующему игроку, который ещё не спасовал.
  /// </summary>
  public void EndTurn()
  {
    RequirePhase(GamePhase.PlayerTurns);
    Turns.EndTurn();
  }

  /// <summary>
  /// Игрок, который сейчас ходит, пасует и до конца раунда больше не ходит. Когда спасуют все,
  /// начинается проверка штабов теорий.
  /// </summary>
  public void Pass()
  {
    RequirePhase(GamePhase.PlayerTurns);
    Turns.Pass();
    if (Turns.AllPassed)
      SetPhase(GamePhase.HeadquartersCheck);
  }

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
  /// Когда все теории получили доход, начинается обновление рынка.
  /// </summary>
  /// <returns>Доход теории или <c>null</c>, если все теории уже получили доход.</returns>
  public TheoryIncome? PayNextTheoryIncome()
  {
    RequirePhase(GamePhase.AdeptIncome);
    if (!_unpaidTheories.TryDequeue(out var column))
    {
      SetPhase(GamePhase.MarketRefresh);
      return null;
    }

    var income = TheoryIncome.Of(column);
    foreach (var (player, amount) in income.ByPlayer)
      player.Earn(amount);
    return income;
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
  }

  /// <summary>
  /// Контекст для действий игрока, который сейчас ходит.
  /// </summary>
  public GameContext CreateContext() => new(CurrentPlayer, Field);

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
