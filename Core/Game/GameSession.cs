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
  /// Начать партию: поле стандартного размера, личные штабы игроков стоят в ряд внизу поля.
  /// </summary>
  /// <param name="players">Игроки в порядке хода.</param>
  public GameSession(IEnumerable<Player> players)
  {
    Turns = new TurnOrder(players);
    Field = new PlayingField(FieldLayout.Columns, FieldLayout.Rows, FieldLayout.CommonZone);
    for (var i = 0; i < Players.Count; i++)
      Field.AddPlayer(Players[i], FieldLayout.PlayerZone(i, Players.Count));
  }

  /// <summary>
  /// Контекст для действий игрока, который сейчас ходит.
  /// </summary>
  public GameContext CreateContext() => new(CurrentPlayer, Field);
}
