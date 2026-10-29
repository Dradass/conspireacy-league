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
  public TheoryColumn? TargetTheory { get; private init; }

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
  public GameContext WithTargetTheory(TheoryColumn theory) => new(CurrentPlayer, Field) { TargetTheory = theory };
}
