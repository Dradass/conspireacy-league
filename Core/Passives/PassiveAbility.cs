using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность карты: срабатывает сама при определённых условиях.
/// </summary>
/// <remarks>
/// Игра оповещает карты о событиях через <see cref="Handle"/>,
/// а способность сама решает, подходит ли ей событие.
/// </remarks>
public abstract class PassiveAbility
{
  /// <summary>
  /// Отсутствующая пассивная способность.
  /// </summary>
  public static PassiveAbility None { get; } = new NoPassiveAbility();

  /// <summary>
  /// Текст способности для отображения на карте.
  /// </summary>
  public abstract string Description { get; }

  /// <summary>
  /// Обработать игровое событие.
  /// </summary>
  /// <param name="gameEvent">Событие.</param>
  /// <param name="context">Контекст партии.</param>
  public abstract void Handle(GameEvent gameEvent, GameContext context);

  public override string ToString() => Description;

  private sealed class NoPassiveAbility : PassiveAbility
  {
    public override string Description => "Отсутствует";

    public override void Handle(GameEvent gameEvent, GameContext context)
    {
    }
  }
}
