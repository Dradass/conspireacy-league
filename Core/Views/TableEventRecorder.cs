using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Views;

/// <summary>
/// Записывает события партии (события <see cref="GameSession"/>, поля, рынка, игроков) как события стола
/// (<see cref="TableEvent"/>) — в том порядке, в каком они происходят.
/// </summary>
/// <remarks>
/// Обработчики ничего не бросают: ошибка в записи не должна ломать ход партии.
/// </remarks>
public sealed class TableEventRecorder
{
  private readonly List<TableEvent> _events = [];
  private readonly Dictionary<Player, int> _seats;

  /// <summary>
  /// Начать записывать события партии.
  /// </summary>
  public TableEventRecorder(GameSession session)
  {
    ArgumentNullException.ThrowIfNull(session);
    _seats = session.Players.Select((player, index) => (player, index)).ToDictionary();
    Subscribe(session);
  }

  /// <summary>
  /// Записанные события.
  /// </summary>
  public IReadOnlyList<TableEvent> Events => _events;

  /// <summary>
  /// Добавить событие, которого нет среди событий партии (например, что игрок сделал командой).
  /// </summary>
  public void Add(TableEvent tableEvent) => _events.Add(tableEvent);

  /// <summary>
  /// Забрать записанные события и начать список заново.
  /// </summary>
  public IReadOnlyList<TableEvent> Drain()
  {
    var drained = _events.ToList();
    _events.Clear();
    return drained;
  }

  /// <summary>
  /// Отбросить события после первых <paramref name="count"/> — они описывают отменённые изменения.
  /// </summary>
  public void Truncate(int count)
  {
    if (count < _events.Count)
      _events.RemoveRange(count, _events.Count - count);
  }

  private int Seat(Player player) => _seats.GetValueOrDefault(player, -1);

  private static IReadOnlyList<CellMove> Moves(IReadOnlyList<(Cell From, Cell To)> moves) =>
    moves.Select(move => new CellMove(move.From, move.To)).ToList();

  private void Subscribe(GameSession session)
  {
    var field = session.Field;
    var market = session.Market;

    session.Turns.TurnChanged += player => Add(new TurnStarted(Seat(player)));
    session.Turns.FirstPlayerChanged += player => Add(new FirstPlayerChanged(Seat(player)));
    session.Turns.Passed += player => Add(new PlayerPassed(Seat(player)));
    session.AutoPassed += player => Add(new PlayerAutoPassed(Seat(player)));
    // Текст о действии — без скрытого: события одинаковы для всех игроков.
    session.ActionTaken += action => Add(new PlayerActed(Seat(action.Player), Logging.ActionText.Describe(action, revealHidden: false)));
    session.PhaseChanged += phase => Add(new PhaseChanged(phase));
    session.LastRoundDeclared += column => Add(new LastRoundDeclared(column.Theory.Name));
    session.PendingCheckChanged += check =>
    {
      if (check is not null)
        Add(new DiceRolled(Seat(session.CurrentPlayer), check.Cell, check.Chance, check.Die, check.Succeeded));
    };

    foreach (var player in session.Players)
    {
      player.MoneyChanged += changed => Add(new MoneyChanged(Seat(changed), changed.Money));
      player.Hand.Changed += () => Add(new HandCountChanged(Seat(player), player.Hand.Cards.Count));
    }

    market.CardOpened += (cell, _) => Add(new MarketCardOpened(cell));
    market.CardTaken += (cell, _) => Add(new MarketCardTaken(cell));
    market.CardDiscarded += (cell, _) => Add(new MarketCardDiscarded(cell));
    market.CardMoved += (from, to, _) => Add(new MarketCardMoved(from, to));

    field.CommonZoneChanged += _ => Add(new CommonZoneGrew());
    field.AdeptsAdded += (column, count) => Add(new AdeptsAdded(column.Theory.Name, count));
    field.AdeptsRemoved += (column, count) => Add(new AdeptsRemoved(column.Theory.Name, count));
    field.AdeptsMoved += (from, to, count) => Add(new AdeptsMoved(from.Theory.Name, to.Theory.Name, count));
    field.CardAdeptsAdded += (cell, count) => Add(new CardAdeptsAdded(cell, count));
    field.TheoryClosed += column => Add(new TheoryClosed(column.Theory.Name));
    field.TheoryReopened += column => Add(new TheoryReopened(column.Theory.Name));
    field.TheoryColumnMoved += (column, moves) => Add(new TheoryMoved(column.Theory.Name, Moves(moves)));
    field.UsedCardsRefreshed += cells => Add(new CardsRefreshed(cells.ToList()));
    field.TheoryCardsRefreshed += _ => Add(new TheoryCardsRefreshed());
    field.HiddenCardRevealed += cell => Add(new HiddenCardRevealed(cell));
    field.HiddenCardPlaced += (cell, player) => Add(new HiddenCardPlaced(Seat(player), cell));
    field.TheoryEarned += (owned, amount) => Add(new TheoryEarned(owned.Card.Theory.Name, Seat(owned.Owner), amount));
    field.LeaderMeepleAdded += (column, player) => Add(new LeaderMeepleAdded(column.Theory.Name, Seat(player)));
    field.LeaderMeeplesRemoved += columns => Add(new LeaderMeeplesRemoved(columns.Select(column => column.Theory.Name).ToList()));
    field.CardEarned += (cell, amount, scientists, theories) => Add(new CardEarned(cell, amount, scientists, theories));
    field.CardTrained += cell => Add(new CardTrained(cell));
    field.PersonalCardDestroyed += cell => Add(new CardDestroyed(cell));
    field.PersonalCardDemoted += cell => Add(new CardDemoted(cell));
    field.CardTakenToHand += cell => Add(new CardTakenToHand(cell));
    field.TheoryCardRemoved += (cell, moves) => Add(new CardRemovedFromTheory(cell, Moves(moves)));
    field.CardReplaced += cell => Add(new CardReplaced(cell));
    field.CardRemoved += cell => Add(new CardRemoved(cell));
    field.CardsSwapped += (first, second) => Add(new CardsSwapped(first, second));
    field.TheoryCardGranted += owned => Add(new TheoryCardGranted(owned.Card.Theory.Name, Seat(owned.Owner)));
    field.TheoryCardTransferred += owned => Add(new TheoryCardTransferred(owned.Card.Theory.Name, Seat(owned.Owner)));
    field.TheoryCardUsed += owned => Add(new TheoryCardUsed(owned.Card.Theory.Name, Seat(owned.Owner)));
    field.CharacterChipped += cell => Add(new CharacterChipped(cell));
    field.WheelMarked += cell => Add(new WheelMarked(cell));
  }

  /// <summary>
  /// Доход от адептов теории.
  /// </summary>
  public void AddIncome(TheoryIncome income) =>
    Add(new TheoryIncomePaid(
      income.Column.Theory.Name,
      income.Total,
      income.ByPlayer.Select(pair => new SeatAmount(Seat(pair.Player), pair.Amount)).ToList()));

  /// <summary>
  /// Деньги за лидеров в итоговом подсчёте.
  /// </summary>
  public void AddBonus(LeadersBonus bonus) => Add(new LeadersBonusPaid(bonus.Column.Theory.Name, Seat(bonus.Owner), bonus.Amount));
}
