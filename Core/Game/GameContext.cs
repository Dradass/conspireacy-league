using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Field;

namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Контекст партии, в котором выполняются эффекты карт.
/// </summary>
/// <remarks>
/// TODO: остальное состояние игры — игроки, рука, штаб теории, а также источник случайности.
/// </remarks>
public sealed class GameContext
{
  /// <summary>
  /// Игрок, для которого выполняются эффекты.
  /// </summary>
  public Player CurrentPlayer { get; }

  /// <summary>
  /// Игровое поле.
  /// </summary>
  public PlayingField Field { get; }

  /// <summary>
  /// Теория, которую игрок выбрал для действия (например, куда добавить адептов),
  /// или штаб теории, в который выложена карточка.
  /// </summary>
  public TheoryColumn? TargetTheory { get; private set; }

  /// <summary>
  /// Все выбранные теории, когда действию нужно несколько (<see cref="Effect.TheoryCount"/>), в порядке выбора.
  /// </summary>
  public IReadOnlyList<TheoryColumn> TargetTheories { get; private set; } = [];

  /// <summary>
  /// Карточка в руке, которую игрок выбрал для действия (например, чтобы выложить её взакрытую).
  /// </summary>
  public Card? HandCard { get; private set; }

  /// <summary>
  /// Ячейка поля, которую игрок выбрал для действия (например, карточку другого игрока, чтобы её перевернуть).
  /// </summary>
  public Cell? TargetCell { get; private set; }

  /// <summary>
  /// Вторая ячейка поля, которую игрок выбрал для действия (<see cref="Effect.RequiresSecondTargetCell"/>).
  /// </summary>
  public Cell? SecondTargetCell { get; private set; }

  /// <summary>
  /// Карточка в личном штабе, на которую приводят адептов (<see cref="PersonCard.AdeptsBonus"/>), —
  /// её игрок выбрал вместо теории.
  /// </summary>
  public Cell? TargetAdeptCard { get; private set; }

  /// <summary>
  /// Карточка на поле, действие которой выполняет другая карточка (<see cref="Effect.BorrowsTargetCard"/>):
  /// чужая — у бандита и актёра, своя — у учёного.
  /// </summary>
  public Cell? BorrowedCell { get; private set; }

  /// <summary>
  /// Вариант действия карточки <see cref="BorrowedCell"/>, который игрок выбрал. Его цели (теория, карточка на поле,
  /// карточка в руке, игрок) — в остальных свойствах контекста.
  /// </summary>
  public Effect? BorrowedOption { get; private set; }

  /// <summary>
  /// Другой игрок, которого игрок выбрал для действия (например, для инспекции).
  /// </summary>
  public Player? TargetPlayer { get; private set; }

  /// <summary>
  /// Теория-источник, которую игрок выбрал для действия (например, откуда забрать адепта).
  /// </summary>
  public TheoryColumn? SourceTheory { get; private set; }

  /// <summary>
  /// Ячейка карточки из личного штаба, которая выполняет действие, или <c>null</c>.
  /// </summary>
  public Cell? SourceCell { get; private set; }

  /// <summary>
  /// Источник случайности.
  /// </summary>
  public Random Random { get; }

  /// <summary>
  /// Рынок или <c>null</c>, если действию он не нужен (например, при выкладывании карточки в штаб теории).
  /// </summary>
  public Market? Market { get; }

  /// <param name="currentPlayer">Игрок, для которого выполняются эффекты.</param>
  /// <param name="field">Игровое поле.</param>
  /// <param name="random">Источник случайности; по умолчанию — общий.</param>
  /// <param name="market">Рынок.</param>
  public GameContext(Player currentPlayer, PlayingField field, Random? random = null, Market? market = null)
  {
    ArgumentNullException.ThrowIfNull(currentPlayer);
    ArgumentNullException.ThrowIfNull(field);
    CurrentPlayer = currentPlayer;
    Field = field;
    Random = random ?? Random.Shared;
    Market = market;
  }

  /// <summary>
  /// Тот же контекст с выбранной игроком теорией.
  /// </summary>
  /// <param name="theory">Выбранная теория.</param>
  public GameContext WithTargetTheory(TheoryColumn theory) => With(context => context.TargetTheory = theory);

  /// <summary>
  /// Тот же контекст с несколькими выбранными теориями; последняя становится <see cref="TargetTheory"/>.
  /// </summary>
  /// <param name="theories">Выбранные теории в порядке выбора.</param>
  public GameContext WithTargetTheories(IReadOnlyList<TheoryColumn> theories) =>
    With(context =>
    {
      context.TargetTheories = theories;
      context.TargetTheory = theories.LastOrDefault();
    });

  /// <summary>
  /// Тот же контекст с выбранной игроком карточкой в руке.
  /// </summary>
  /// <param name="card">Выбранная карточка.</param>
  public GameContext WithHandCard(Card card) => With(context => context.HandCard = card);

  /// <summary>
  /// Тот же контекст с выбранной игроком ячейкой поля.
  /// </summary>
  /// <param name="cell">Выбранная ячейка.</param>
  public GameContext WithTargetCell(Cell cell) => With(context => context.TargetCell = cell);

  /// <summary>
  /// Тот же контекст со второй выбранной игроком ячейкой поля.
  /// </summary>
  /// <param name="cell">Выбранная ячейка.</param>
  public GameContext WithSecondTargetCell(Cell cell) => With(context => context.SecondTargetCell = cell);

  /// <summary>
  /// Тот же контекст с выбранной вместо теории карточкой в личном штабе, на которую приводят адептов.
  /// </summary>
  /// <param name="cell">Ячейка карточки.</param>
  public GameContext WithTargetAdeptCard(Cell cell) => With(context => context.TargetAdeptCard = cell);

  /// <summary>
  /// Тот же контекст с карточкой, действие которой выполняет другая карточка.
  /// </summary>
  /// <param name="cell">Ячейка карточки.</param>
  public GameContext WithBorrowedCell(Cell cell) => With(context => context.BorrowedCell = cell);

  /// <summary>
  /// Тот же контекст с выбранным вариантом действия чужой карточки.
  /// </summary>
  /// <param name="option">Вариант действия.</param>
  public GameContext WithBorrowedOption(Effect option) => With(context => context.BorrowedOption = option);

  /// <summary>
  /// Тот же контекст с выбранным другим игроком.
  /// </summary>
  /// <param name="player">Выбранный игрок.</param>
  public GameContext WithTargetPlayer(Player player) => With(context => context.TargetPlayer = player);

  /// <summary>
  /// Тот же контекст с выбранной теорией-источником.
  /// </summary>
  /// <param name="theory">Теория-источник.</param>
  public GameContext WithSourceTheory(TheoryColumn theory) => With(context => context.SourceTheory = theory);

  /// <summary>
  /// Тот же контекст с карточкой, которая выполняет действие.
  /// </summary>
  /// <param name="cell">Ячейка карточки.</param>
  public GameContext WithSourceCell(Cell cell) => With(context => context.SourceCell = cell);

  private GameContext With(Action<GameContext> change)
  {
    var copy = (GameContext)MemberwiseClone();
    change(copy);
    return copy;
  }
}
