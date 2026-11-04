using ConspiracyLeague.Core.Cards;

namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Карточки в руке игрока.
/// </summary>
public sealed class Hand
{
  private readonly List<Card> _cards = [];

  /// <summary>
  /// Карточки в том порядке, в каком они попали в руку.
  /// </summary>
  public IReadOnlyList<Card> Cards => _cards;

  /// <summary>
  /// Карточки в руке изменились.
  /// </summary>
  public event Action? Changed;

  /// <summary>
  /// Есть ли карточка в руке.
  /// </summary>
  public bool Contains(Card card) => _cards.Contains(card);

  internal void Add(Card card)
  {
    ArgumentNullException.ThrowIfNull(card);
    _cards.Add(card);
    Changed?.Invoke();
  }

  internal void Remove(Card card)
  {
    if (!_cards.Remove(card))
      throw new InvalidOperationException($"Карточки «{card}» нет в руке.");
    Changed?.Invoke();
  }

  /// <summary>
  /// Заменить карточку в руке на другую на том же месте.
  /// </summary>
  internal void Replace(Card card, Card replacement)
  {
    ArgumentNullException.ThrowIfNull(replacement);
    var index = _cards.IndexOf(card);
    if (index < 0)
      throw new InvalidOperationException($"Карточки «{card}» нет в руке.");
    _cards[index] = replacement;
    Changed?.Invoke();
  }
}
