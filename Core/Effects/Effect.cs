using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Действие карты: «получить 2$», «добавить адепта» и т.п.
/// </summary>
/// <remarks>
/// Эффекты — маленькие переиспользуемые объекты. Карты собираются из них,
/// а не переопределяют методы, поэтому новая карта — это чаще всего новая комбинация
/// существующих эффектов, а не новый код.
/// </remarks>
public abstract class Effect
{
  /// <summary>
  /// Отсутствующее действие.
  /// </summary>
  public static Effect None { get; } = new NoEffect();

  /// <summary>
  /// Текст действия для отображения на карте.
  /// </summary>
  public abstract string Description { get; }

  /// <summary>
  /// Варианты, из которых игрок выбирает одно действие. У обычного действия вариант один — оно само.
  /// </summary>
  public virtual IReadOnlyList<Effect> Options => [this];

  /// <summary>
  /// Нужно ли перед выполнением выбрать теорию (<see cref="GameContext.TargetTheory"/>).
  /// </summary>
  public virtual bool RequiresTheory => false;

  /// <summary>
  /// Нужно ли перед выполнением выбрать карточку в руке (<see cref="GameContext.HandCard"/>).
  /// </summary>
  public virtual bool RequiresHandCard => false;

  /// <summary>
  /// Нужно ли перед выполнением выбрать карточку в личном штабе другого игрока (<see cref="GameContext.TargetCell"/>).
  /// </summary>
  public virtual bool RequiresRivalCard => false;

  /// <summary>
  /// Выполнить действие.
  /// </summary>
  /// <param name="context">Контекст партии.</param>
  public abstract void Execute(GameContext context);

  public override string ToString() => Description;

  private sealed class NoEffect : Effect
  {
    public override string Description => "Отсутствует";

    public override void Execute(GameContext context)
    {
    }
  }
}
