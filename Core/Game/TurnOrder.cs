namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Игроки партии и очерёдность их ходов.
/// </summary>
/// <remarks>
/// Игрок, сказавший «пас», до конца раунда больше не ходит: ход его пропускает.
/// </remarks>
public sealed class TurnOrder
{
  private readonly List<Player> _players;
  private readonly HashSet<Player> _passed = [];
  private int _currentIndex;
  private int _firstIndex;

  /// <summary>
  /// Игроки в порядке хода.
  /// </summary>
  public IReadOnlyList<Player> Players => _players;

  /// <summary>
  /// Игрок, который сейчас ходит.
  /// </summary>
  public Player CurrentPlayer => _players[_currentIndex];

  /// <summary>
  /// Игрок с жетоном первого игрока: он начинает раунд.
  /// </summary>
  public Player FirstPlayer => _players[_firstIndex];

  /// <summary>
  /// В начале раунда жетон первого игрока выдан; аргумент — первый игрок раунда.
  /// </summary>
  public event Action<Player>? FirstPlayerChanged;

  /// <summary>
  /// Ход перешёл к другому игроку; аргумент — игрок, который теперь ходит.
  /// </summary>
  public event Action<Player>? TurnChanged;

  /// <summary>
  /// Игрок спасовал; аргумент — этот игрок.
  /// </summary>
  public event Action<Player>? Passed;

  /// <summary>
  /// Спасовали все игроки — ходы в этом раунде закончились.
  /// </summary>
  public bool AllPassed => _passed.Count == _players.Count;

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
  /// Спасовал ли игрок в этом раунде.
  /// </summary>
  public bool HasPassed(Player player) => _passed.Contains(player);

  /// <summary>
  /// Передать ход следующему игроку, который ещё не спасовал.
  /// </summary>
  /// <exception cref="InvalidOperationException">Все игроки спасовали.</exception>
  public void EndTurn()
  {
    if (AllPassed)
      throw new InvalidOperationException("Все игроки спасовали — ходы в этом раунде закончились.");

    do
      _currentIndex = (_currentIndex + 1) % _players.Count;
    while (HasPassed(CurrentPlayer));
    TurnChanged?.Invoke(CurrentPlayer);
  }

  /// <summary>
  /// Игрок, который сейчас ходит, пасует: до конца раунда он больше не ходит. Ход переходит к следующему,
  /// если ещё не все спасовали.
  /// </summary>
  public void Pass()
  {
    var player = CurrentPlayer;
    if (!_passed.Add(player))
      throw new InvalidOperationException($"Игрок {player} уже спасовал.");

    Passed?.Invoke(player);
    if (!AllPassed)
      EndTurn();
  }

  /// <summary>
  /// Следующий игрок по кругу (направо) после данного.
  /// </summary>
  public Player NextAfter(Player player) => _players[(IndexOf(player) + 1) % _players.Count];

  /// <summary>
  /// Начать новый раунд: пасы сбрасываются, жетон первого игрока получает <paramref name="first"/>, и он ходит первым.
  /// </summary>
  /// <param name="first">Первый игрок раунда.</param>
  public void StartRound(Player first)
  {
    var firstIndex = IndexOf(first);
    _passed.Clear();
    _firstIndex = firstIndex;
    _currentIndex = firstIndex;
    FirstPlayerChanged?.Invoke(FirstPlayer);
    TurnChanged?.Invoke(CurrentPlayer);
  }

  /// <summary>
  /// Клиент: восстановить очередь по присланному сервером виду (<see cref="Client.ClientMirror"/>). Событий не вызывает.
  /// </summary>
  internal void Restore(int current, int first, IEnumerable<Player> passed)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(current);
    ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(current, _players.Count);
    ArgumentOutOfRangeException.ThrowIfNegative(first);
    ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(first, _players.Count);
    _currentIndex = current;
    _firstIndex = first;
    _passed.Clear();
    _passed.UnionWith(passed);
  }

  private int IndexOf(Player player)
  {
    var index = _players.IndexOf(player);
    if (index < 0)
      throw new ArgumentException($"Игрок {player} не участвует в партии.", nameof(player));
    return index;
  }
}
