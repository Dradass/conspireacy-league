using ConspiracyLeague.Core.Cards;
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
  /// Нужно ли перед выполнением выбрать другого игрока (<see cref="GameContext.TargetPlayer"/>).
  /// </summary>
  public virtual bool RequiresTargetPlayer => false;

  /// <summary>
  /// Быстрое действие: не тратит действие хода, а карточка после него не переворачивается, а поворачивается
  /// на 90 градусов и может сыграть ещё одно любое действие (<see cref="Field.PlayingField.IsRotated"/>).
  /// </summary>
  public virtual bool IsQuick => false;

  /// <summary>
  /// Нужно ли перед выполнением выбрать теорию (<see cref="GameContext.TargetTheory"/>).
  /// </summary>
  public virtual bool RequiresTheory => false;

  /// <summary>
  /// Сколько теорий нужно выбрать для действия (<see cref="GameContext.TargetTheories"/>). Больше одной — например,
  /// «добавь адептов в любые 3 теории».
  /// </summary>
  public virtual int TheoryCount => 1;

  /// <summary>
  /// Можно ли при выборе нескольких теорий (<see cref="TheoryCount"/>) выбрать одну и ту же теорию несколько раз.
  /// </summary>
  public virtual bool AllowsRepeatedTheories => false;

  /// <summary>
  /// Можно ли выбрать для действия эту теорию, если действию нужна теория.
  /// </summary>
  /// <param name="column">Теория.</param>
  /// <param name="player">Игрок, который выполняет действие.</param>
  public virtual bool CanTargetTheory(TheoryColumn column, Player player) => !column.IsClosed;

  /// <summary>
  /// Нужно ли перед выполнением выбрать ещё и теорию-источник (<see cref="GameContext.SourceTheory"/>) —
  /// например, откуда забрать адепта. Теория-цель (<see cref="GameContext.TargetTheory"/>) выбирается после неё
  /// и должна быть другой.
  /// </summary>
  public virtual bool RequiresSourceTheory => false;

  /// <summary>
  /// Можно ли выбрать эту теорию источником (<see cref="RequiresSourceTheory"/>).
  /// </summary>
  /// <param name="column">Теория.</param>
  /// <param name="player">Игрок, который выполняет действие.</param>
  public virtual bool CanTargetSourceTheory(TheoryColumn column, Player player) => false;

  /// <summary>
  /// Можно ли вместо теории выбрать свою карточку в личном штабе, на которую приводят адептов
  /// (<see cref="GameContext.TargetAdeptCard"/>).
  /// </summary>
  /// <param name="cell">Ячейка карточки.</param>
  /// <param name="field">Игровое поле.</param>
  public virtual bool CanTargetAdeptCard(Cell cell, PlayingField field) => false;

  /// <summary>
  /// Нужно ли перед выполнением выбрать карточку в руке (<see cref="GameContext.HandCard"/>).
  /// </summary>
  public virtual bool RequiresHandCard => false;

  /// <summary>
  /// Нужно ли перед выполнением выбрать карточку на поле (<see cref="GameContext.TargetCell"/>).
  /// </summary>
  public virtual bool RequiresTargetCell => false;

  /// <summary>
  /// Вместо карточки на поле можно выбрать карточку в руке (<see cref="GameContext.HandCard"/>), если она подходит
  /// (<see cref="CheckHandCard"/>). Тогда цель на поле не нужна.
  /// </summary>
  public virtual bool AcceptsHandCardTarget => false;

  /// <summary>
  /// Можно ли выбрать карточку в руке вместо карточки на поле (<see cref="AcceptsHandCardTarget"/>).
  /// </summary>
  /// <param name="card">Карточка в руке.</param>
  /// <param name="context">Контекст партии.</param>
  public virtual PlacementCheck CheckHandCard(Card card, GameContext context) => PlacementCheck.Allowed;

  /// <summary>
  /// Нужно ли после первой карточки на поле выбрать вторую (<see cref="GameContext.SecondTargetCell"/>).
  /// Для неё тоже вызываются <see cref="TargetCells"/> и <see cref="CheckTargetCell"/> — уже с первой в контексте.
  /// </summary>
  public virtual bool RequiresSecondTargetCell => false;

  /// <summary>
  /// Карточку на поле можно не выбирать — тогда действие выполняется без неё (так устроены действия
  /// при выкладывании в штаб теории: «можешь …»).
  /// </summary>
  public virtual bool TargetIsOptional => false;

  /// <summary>
  /// Действие использует карточку на поле как свою: выбранная карточка (<see cref="RequiresTargetCell"/>) попадает
  /// в <see cref="GameContext.BorrowedCell"/>, затем игрок выбирает один из вариантов её действия
  /// (<see cref="GameContext.BorrowedOption"/>) и его цели.
  /// </summary>
  public virtual bool BorrowsTargetCard => false;

  /// <summary>
  /// Можно ли выполнить этот вариант через другую карточку (<see cref="BorrowsTargetCard"/>): можно любой,
  /// кроме тех, что сами используют другую карточку.
  /// </summary>
  public virtual bool CanBeBorrowed => !BorrowsTargetCard;

  /// <summary>
  /// Карточки на поле, которые подходят для действия, с условиями выбора: можно ли и сколько $ это стоит.
  /// Учитывает плату владельцу чужого полицейского (<see cref="PlayingField.WithPolicemanToll"/>).
  /// </summary>
  /// <param name="context">Контекст партии.</param>
  public IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCells(GameContext context) =>
    TargetCellsCore(context).Select(target => (target.Cell, context.Field.WithPolicemanToll(target.Check, target.Cell, this, context)));

  /// <summary>
  /// Проверить, можно ли выбрать карточку на поле для действия.
  /// Учитывает плату владельцу чужого полицейского (<see cref="PlayingField.WithPolicemanToll"/>).
  /// </summary>
  /// <param name="cell">Ячейка карточки.</param>
  /// <param name="context">Контекст партии.</param>
  public PlacementCheck CheckTargetCell(Cell cell, GameContext context) =>
    context.Field.WithPolicemanToll(CheckTargetCellCore(cell, context), cell, this, context);

  /// <summary>
  /// Карточки на поле, которые подходят для действия, без учёта платы полицейскому (см. <see cref="TargetCells"/>).
  /// </summary>
  /// <param name="context">Контекст партии.</param>
  protected internal virtual IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCellsCore(GameContext context) => [];

  /// <summary>
  /// Проверить карточку на поле без учёта платы полицейскому (см. <see cref="CheckTargetCell"/>).
  /// </summary>
  /// <param name="cell">Ячейка карточки.</param>
  /// <param name="context">Контекст партии.</param>
  protected internal virtual PlacementCheck CheckTargetCellCore(Cell cell, GameContext context) =>
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
