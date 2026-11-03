using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Field;

/// <summary>
/// Карточка теории у текущего владельца теории. Лежит в ряду карточек теорий под рабочей зоной владельца.
/// Владелец получает её, когда теория стала действующей с достаточным числом лидеров. Когда владелец теории меняется,
/// карточка переходит новому владельцу в текущем состоянии (применена она или нет).
/// </summary>
public sealed class OwnedTheoryCard
{
  /// <summary>
  /// Карточка теории.
  /// </summary>
  public TheoryCard Card { get; }

  /// <summary>
  /// Текущий владелец карточки — владелец теории.
  /// </summary>
  public Player Owner { get; internal set; }

  /// <summary>
  /// Ячейка в ряду карточек теорий владельца.
  /// </summary>
  public Cell Cell { get; internal set; }

  /// <summary>
  /// Карточка применена и лежит рубашкой вверх до обновления карточек.
  /// </summary>
  public bool IsUsed { get; internal set; }

  /// <summary>
  /// Ячейка персонажа, на которого положена карточка «Шапочки из фольги», или <c>null</c>.
  /// </summary>
  public Cell? AttachedTo { get; internal set; }

  internal OwnedTheoryCard(TheoryCard card, Player owner, Cell cell)
  {
    Card = card;
    Owner = owner;
    Cell = cell;
  }

  public override string ToString() => Card.Name;
}
