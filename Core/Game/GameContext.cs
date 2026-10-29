using ConspiracyLeague.Core.Cards;
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
  /// Карточка в руке, которую игрок выбрал для действия (например, чтобы выложить её взакрытую).
  /// </summary>
  public Card? HandCard { get; private set; }

  /// <summary>
  /// Ячейка поля, которую игрок выбрал для действия (например, карточку другого игрока, чтобы её перевернуть).
  /// </summary>
  public Cell? TargetCell { get; private set; }

  /// <param name="currentPlayer">Игрок, для которого выполняются эффекты.</param>
  /// <param name="field">Игровое поле.</param>
  public GameContext(Player currentPlayer, PlayingField field)
  {
    ArgumentNullException.ThrowIfNull(currentPlayer);
    ArgumentNullException.ThrowIfNull(field);
    CurrentPlayer = currentPlayer;
    Field = field;
  }

  /// <summary>
  /// Тот же контекст с выбранной игроком теорией.
  /// </summary>
  /// <param name="theory">Выбранная теория.</param>
  public GameContext WithTargetTheory(TheoryColumn theory) => With(context => context.TargetTheory = theory);

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

  private GameContext With(Action<GameContext> change)
  {
    var copy = (GameContext)MemberwiseClone();
    change(copy);
    return copy;
  }
}
