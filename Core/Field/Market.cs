using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Field;

/// <summary>
/// Рынок: колода рынка и открытые из неё карточки.
/// </summary>
/// <remarks>
/// Колода лежит во втором столбце второй строки зоны рынка. Открытые карточки выкладываются в первую
/// и третью строки, начиная с пятого столбца. Открытую карточку можно купить за её стоимость — она уходит в руку.
/// В конце раунда рынок обновляется: нижняя строка уходит, верхняя сдвигается вниз, пустые места заполняются из колоды.
/// Дополнительные карточки (<see cref="OpenExtraCards"/>) открываются во второй строке, правее колоды,
/// и уходят при обновлении вместе с нижней строкой.
/// </remarks>
public sealed class Market
{
  private const int FirstOpenColumn = 4;

  private readonly Stack<PersonCard> _deck;
  private readonly Dictionary<Cell, PersonCard> _openCards = new();
  private int _slotsPerRow;

  /// <summary>
  /// Зона рынка.
  /// </summary>
  public Zone Zone { get; }

  /// <summary>
  /// Ячейка колоды рынка.
  /// </summary>
  public Cell DeckCell => new(Zone.Left + 1, Zone.Top + 1);

  /// <summary>
  /// Сколько карточек осталось в колоде.
  /// </summary>
  public int DeckCount => _deck.Count;

  /// <summary>
  /// Открытые карточки рынка.
  /// </summary>
  public IReadOnlyDictionary<Cell, PersonCard> OpenCards => _openCards;

  /// <summary>
  /// Из колоды открыта карточка; аргументы — ячейка, куда она выложена, и карточка.
  /// </summary>
  public event Action<Cell, PersonCard>? CardOpened;

  /// <summary>
  /// Открытую карточку купили; аргументы — ячейка, где она лежала, и карточка.
  /// </summary>
  public event Action<Cell, PersonCard>? CardTaken;

  /// <summary>
  /// Карточка ушла с рынка при обновлении; аргументы — ячейка, где она лежала, и карточка.
  /// </summary>
  public event Action<Cell, PersonCard>? CardDiscarded;

  /// <summary>
  /// Карточка сдвинулась при обновлении; аргументы — откуда, куда и карточка.
  /// </summary>
  public event Action<Cell, Cell, PersonCard>? CardMoved;

  /// <param name="zone">Зона рынка.</param>
  /// <param name="deck">Колода рынка; первая карточка — верхняя.</param>
  public Market(Zone zone, IReadOnlyList<PersonCard> deck)
  {
    ArgumentNullException.ThrowIfNull(zone);
    ArgumentNullException.ThrowIfNull(deck);
    Zone = zone;
    _deck = new Stack<PersonCard>(deck.Reverse());
  }

  /// <summary>
  /// Входит ли ячейка в рынок.
  /// </summary>
  public bool Contains(Cell cell) => Zone.Contains(cell);

  /// <summary>
  /// Открыть стартовые карточки: в первую и в третью строки зоны, начиная с пятого столбца, по <paramref name="perRow"/> карточек.
  /// </summary>
  /// <param name="perRow">Сколько карточек открыть в каждую строку.</param>
  public void OpenStartingCards(int perRow)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(perRow);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(perRow, Zone.Width - FirstOpenColumn);

    _slotsPerRow = perRow;
    foreach (var cell in TopRow.Concat(BottomRow))
      Open(cell);
  }

  /// <summary>
  /// Обновить рынок: карточки нижней строки уходят, верхняя строка сдвигается вниз, пустые места
  /// заполняются из колоды, пока она не кончится.
  /// </summary>
  public void Refresh()
  {
    foreach (var cell in BottomRow.Concat(ExtraRow))
    {
      if (_openCards.Remove(cell, out var card))
        CardDiscarded?.Invoke(cell, card);
    }

    foreach (var from in TopRow)
    {
      if (!_openCards.Remove(from, out var card))
        continue;

      var to = from.Below.Below;
      _openCards[to] = card;
      CardMoved?.Invoke(from, to, card);
    }

    foreach (var cell in BottomRow.Concat(TopRow).Where(cell => !_openCards.ContainsKey(cell)).ToList())
    {
      if (DeckCount == 0)
        break;
      Open(cell);
    }
  }

  /// <summary>
  /// Открыть дополнительные карточки из колоды во второй строке рынка, правее колоды, — на свободные места.
  /// </summary>
  /// <param name="count">Сколько карточек открыть.</param>
  /// <returns>Сколько открыто: меньше, если колода кончилась или нет свободных мест.</returns>
  public int OpenExtraCards(int count)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(count);
    var free = ExtraRow.Where(cell => !_openCards.ContainsKey(cell)).Take(Math.Min(count, DeckCount)).ToList();
    foreach (var cell in free)
      Open(cell);
    return free.Count;
  }

  /// <summary>
  /// Проверить, может ли игрок купить карточку из ячейки рынка.
  /// </summary>
  /// <returns>Можно ли купить и сколько $ это стоит.</returns>
  public PlacementCheck CheckBuy(Cell cell, Player player)
  {
    if (!_openCards.TryGetValue(cell, out var card))
      return PlacementCheck.Deny("Здесь нет открытой карточки рынка.");

    return player.CanAfford(card.Cost)
      ? PlacementCheck.Allow(card.Cost)
      : PlacementCheck.Deny($"Недостаточно денег: «{card}» стоит {card.Cost}$, а у вас {player.Money}$.");
  }

  /// <summary>
  /// Купить карточку из ячейки рынка: игрок платит её стоимость, карточка уходит к нему в руку.
  /// </summary>
  /// <returns>Купленная карточка.</returns>
  /// <exception cref="InvalidOperationException">Карточку нельзя купить.</exception>
  public PersonCard Buy(Cell cell, Player player)
  {
    var check = CheckBuy(cell, player);
    if (!check.IsAllowed)
      throw new InvalidOperationException(check.Reason);

    var card = _openCards[cell];
    player.Spend(check.Cost);
    _openCards.Remove(cell);
    player.Hand.Add(card);
    CardTaken?.Invoke(cell, card);
    return card;
  }

  /// <summary>
  /// Есть ли в колоде карточка, подходящая под условие.
  /// </summary>
  public bool DeckContains(Func<PersonCard, bool> match) => _deck.Any(match);

  /// <summary>
  /// Вытащить из колоды первую сверху карточку, подходящую под условие. Порядок остальных не меняется.
  /// </summary>
  /// <returns>Карточка или <c>null</c>, если подходящей нет.</returns>
  internal PersonCard? DrawFirst(Func<PersonCard, bool> match)
  {
    var cards = _deck.ToList();
    var index = cards.FindIndex(card => match(card));
    if (index < 0)
      return null;

    var card = cards[index];
    cards.RemoveAt(index);
    _deck.Clear();
    for (var i = cards.Count - 1; i >= 0; i--)
      _deck.Push(cards[i]);
    return card;
  }

  private IEnumerable<Cell> TopRow => Slots(Zone.Top);

  private IEnumerable<Cell> BottomRow => Slots(Zone.Top + 2);

  /// <summary>
  /// Места для дополнительных карточек: вторая строка зоны, правее колоды, до правого края.
  /// </summary>
  private IEnumerable<Cell> ExtraRow =>
    Enumerable.Range(Zone.Left + FirstOpenColumn, Zone.Width - FirstOpenColumn).Select(column => new Cell(column, Zone.Top + 1));

  private IEnumerable<Cell> Slots(int row) =>
    Enumerable.Range(Zone.Left + FirstOpenColumn, _slotsPerRow).Select(column => new Cell(column, row));

  private void Open(Cell cell)
  {
    if (_openCards.ContainsKey(cell))
      throw new InvalidOperationException($"В ячейке {cell} рынка уже лежит карточка.");
    if (!_deck.TryPop(out var card))
      throw new InvalidOperationException("Колода рынка закончилась.");

    _openCards[cell] = card;
    CardOpened?.Invoke(cell, card);
  }
}
