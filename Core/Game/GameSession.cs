using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Decks;
using ConspiracyLeague.Core.Field;

namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Сессия игры — всё состояние партии: игроки и их балансы, очерёдность ходов, поле с личными штабами
/// и выложенные теории (какие карточки в каждой теории, кто их владельцы, сколько адептов).
/// </summary>
public sealed class GameSession
{
  /// <summary>
  /// Игроки и очерёдность их ходов.
  /// </summary>
  public TurnOrder Turns { get; }

  /// <summary>
  /// Игровое поле.
  /// </summary>
  public PlayingField Field { get; }

  /// <summary>
  /// Рынок: колода рынка и открытые карточки.
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
  /// Подготовить партию: поле стандартного размера, личные штабы игроков стоят в ряд внизу поля,
  /// на рынок выложена стартовая колода (<see cref="MarketDeck"/>).
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
  /// Начать партию: открыть из колоды рынка по карточке на игрока в первую и третью строки рынка.
  /// </summary>
  public void Start() => Market.OpenStartingCards(Players.Count);

  /// <summary>
  /// Игрок, который сейчас ходит, покупает карточку с рынка в руку.
  /// </summary>
  /// <param name="cell">Ячейка рынка.</param>
  /// <returns>Купленная карточка.</returns>
  public PersonCard BuyFromMarket(Cell cell) => Market.Buy(cell, CurrentPlayer);

  /// <summary>
  /// Игрок, который сейчас ходит, выкладывает карточку с руки на поле — в личный штаб или в общую зону.
  /// </summary>
  /// <returns>Условия, на которых карточка положена.</returns>
  /// <exception cref="InvalidOperationException">Карточки нет в руке или её сюда класть нельзя.</exception>
  public PlacementCheck PlaceFromHand(Card card, Cell cell)
  {
    var hand = CurrentPlayer.Hand;
    if (!hand.Contains(card))
      throw new InvalidOperationException($"Карточки «{card}» нет в руке игрока {CurrentPlayer}.");

    var check = Field.Place(card, cell, CurrentPlayer);
    hand.Remove(card);
    return check;
  }

  /// <summary>
  /// Контекст для действий игрока, который сейчас ходит.
  /// </summary>
  public GameContext CreateContext() => new(CurrentPlayer, Field);
}
