namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Игроки партии и очерёдность их ходов.
/// </summary>
public sealed class TurnOrder
{
  private readonly List<Player> _players;
  private int _currentIndex;

  /// <summary>
  /// Игроки в порядке хода.
  /// </summary>
  public IReadOnlyList<Player> Players => _players;

  /// <summary>
  /// Игрок, который сейчас ходит.
  /// </summary>
  public Player CurrentPlayer => _players[_currentIndex];

  /// <summary>
  /// Ход перешёл к другому игроку; аргумент — игрок, который теперь ходит.
  /// </summary>
  public event Action<Player>? TurnChanged;

  /// <param name="players">Игроки в порядке хода; первый ходит первым.</param>
  public TurnOrder(IEnumerable<Player> players)
  {
    ArgumentNullException.ThrowIfNull(players);
    _players = players.ToList();
    if (_players.Count == 0)
      throw new ArgumentException("В партии должен быть хотя бы один игрок.", nameof(players));
    if (_players.Distinct().Count() != _players.Count)
      throw new ArgumentException("Игрок не может участвовать в партии дважды.", nameof(players));
    if (_players.Select(player => player.Color).Distinct().Count() != _players.Count)
      throw new ArgumentException("У каждого игрока должен быть свой цвет.", nameof(players));
  }

  /// <summary>
  /// Передать ход следующему игроку.
  /// </summary>
  public void EndTurn()
  {
    _currentIndex = (_currentIndex + 1) % _players.Count;
    TurnChanged?.Invoke(CurrentPlayer);
  }
}
