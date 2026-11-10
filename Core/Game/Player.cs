namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Игрок.
/// </summary>
public sealed class Player
{
  /// <summary>
  /// Имя игрока.
  /// </summary>
  public string Name { get; }

  /// <summary>
  /// Цвет игрока.
  /// </summary>
  public PlayerColor Color { get; }

  /// <summary>
  /// Игроком управляет бот (<see cref="Bots.Bot"/>), а не человек.
  /// </summary>
  public bool IsBot { get; init; }

  /// <summary>
  /// Баланс в $.
  /// </summary>
  public int Money { get; private set; }

  /// <summary>
  /// Карточки в руке.
  /// </summary>
  public Hand Hand { get; } = new();

  /// <summary>
  /// Всё, что игрок получил за партию, по источникам: название карточки, теории или «Прочее».
  /// </summary>
  public IReadOnlyDictionary<string, int> EarnedFrom => _earnedFrom;

  /// <summary>
  /// Источник, который записывается в <see cref="EarnedFrom"/>, пока открыт <see cref="EarningFrom"/>.
  /// </summary>
  private const string OtherSource = "Прочее";

  private readonly Dictionary<string, int> _earnedFrom = new();
  private string? _source;

  /// <summary>
  /// Баланс изменился.
  /// </summary>
  public event Action<Player>? MoneyChanged;

  /// <summary>
  /// Игрок получил деньги; аргументы — игрок и сумма.
  /// </summary>
  public event Action<Player, int>? Earned;

  /// <param name="name">Имя.</param>
  /// <param name="color">Цвет.</param>
  /// <param name="money">Начальный баланс в $.</param>
  public Player(string name, PlayerColor color, int money)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    ArgumentOutOfRangeException.ThrowIfNegative(money);
    Name = name;
    Color = color;
    Money = money;
  }

  /// <summary>
  /// Хватает ли денег.
  /// </summary>
  /// <param name="amount">Сумма в $.</param>
  public bool CanAfford(int amount) => Money >= amount;

  /// <summary>
  /// Получить деньги. Сумма записывается в <see cref="EarnedFrom"/> под текущим источником.
  /// </summary>
  /// <param name="amount">Сумма в $.</param>
  public void Earn(int amount)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(amount);
    ChangeMoney(amount);
    if (amount > 0)
    {
      var source = _source ?? OtherSource;
      _earnedFrom[source] = _earnedFrom.GetValueOrDefault(source) + amount;
      Earned?.Invoke(this, amount);
    }
  }

  /// <summary>
  /// Вернуть деньги, которые были списаны по ошибке (например, в режиме разработчика). Не считается доходом.
  /// </summary>
  /// <param name="amount">Сумма в $.</param>
  public void Refund(int amount)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(amount);
    ChangeMoney(amount);
  }

  /// <summary>
  /// Все деньги, которые игрок получит, пока открыт результат, записываются под этим источником.
  /// Закрывается через <c>using</c>; внутренние источники перекрывают внешние.
  /// </summary>
  /// <param name="source">Название источника: карточка, теория, «Адепты: …».</param>
  public IDisposable EarningFrom(string source)
  {
    var previous = _source;
    _source = source;
    return new SourceScope(() => _source = previous);
  }

  private sealed class SourceScope(Action restore) : IDisposable
  {
    public void Dispose() => restore();
  }

  /// <summary>
  /// Заплатить.
  /// </summary>
  /// <param name="amount">Сумма в $.</param>
  public void Spend(int amount)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(amount);
    if (!CanAfford(amount))
      throw new InvalidOperationException($"Недостаточно денег: нужно {amount}$, есть {Money}$.");
    ChangeMoney(-amount);
  }

  private void ChangeMoney(int delta)
  {
    if (delta == 0)
      return;

    Money += delta;
    MoneyChanged?.Invoke(this);
  }

  public override string ToString() => Name;
}
