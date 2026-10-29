using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Field;

/// <summary>
/// Рынок: колода рынка и открытые из неё карточки.
/// </summary>
/// <remarks>
/// Колода лежит во втором столбце второй строки зоны рынка. Открытые карточки выкладываются в первую
/// и третью строки, начиная с пятого столбца. Открытую карточку можно купить за её стоимость — она уходит в руку.
/// </remarks>
public sealed class Market
{
  private const int FirstOpenColumn = 4;

  private readonly Stack<PersonCard> _deck;
  private readonly Dictionary<Cell, PersonCard> _openCards = new();

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

    foreach (var row in new[] { Zone.Top, Zone.Top + 2 })
    {
      for (var i = 0; i < perRow; i++)
        Open(new Cell(Zone.Left + FirstOpenColumn + i, row));
    }
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
