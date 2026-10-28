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
  /// Баланс в $.
  /// </summary>
  public int Money { get; private set; }

  /// <summary>
  /// Баланс изменился.
  /// </summary>
  public event Action<Player>? MoneyChanged;

  /// <param name="name">Имя.</param>
  /// <param name="money">Начальный баланс в $.</param>
  public Player(string name, int money)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    ArgumentOutOfRangeException.ThrowIfNegative(money);
    Name = name;
    Money = money;
  }

  /// <summary>
  /// Хватает ли денег.
  /// </summary>
  /// <param name="amount">Сумма в $.</param>
  public bool CanAfford(int amount) => Money >= amount;

  /// <summary>
  /// Получить деньги.
  /// </summary>
  /// <param name="amount">Сумма в $.</param>
  public void Earn(int amount)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(amount);
    ChangeMoney(amount);
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
