using ConspiracyLeague.Core.Field;
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
  /// Нужно ли перед выполнением выбрать карточку на поле (<see cref="GameContext.TargetCell"/>).
  /// </summary>
  public virtual bool RequiresTargetCell => false;

  /// <summary>
  /// Карточки на поле, которые подходят для действия, с условиями выбора: можно ли и сколько $ это стоит.
  /// </summary>
  /// <param name="context">Контекст партии.</param>
  public virtual IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCells(GameContext context) => [];

  /// <summary>
  /// Проверить, можно ли выбрать карточку на поле для действия.
  /// </summary>
  /// <param name="cell">Ячейка карточки.</param>
  /// <param name="context">Контекст партии.</param>
  public virtual PlacementCheck CheckTargetCell(Cell cell, GameContext context) =>
    PlacementCheck.Deny($"Действию «{Description}» не нужна карточка на поле.");

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
