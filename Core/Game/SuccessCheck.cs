using ConspiracyLeague.Core.Field;

namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Проверка успеха персонажа, которого играют из личного штаба.
/// </summary>
/// <remarks>
/// Бросается двадцатигранник, выпавшее значение умножается на <see cref="RollStep"/> — получается число от 5 до 100.
/// Проверка успешна, если оно не меньше успешности персонажа.
/// </remarks>
/// <param name="Cell">Ячейка карточки.</param>
/// <param name="Chance">Успешность персонажа (0–100).</param>
/// <param name="Roll">Результат проверки (5–100).</param>
public sealed record SuccessCheck(Cell Cell, int Chance, int Roll)
{
  /// <summary>
  /// Максимальный результат проверки.
  /// </summary>
  public const int MaxRoll = 100;

  /// <summary>
  /// Сколько граней у кубика.
  /// </summary>
  public const int DieSides = 20;

  /// <summary>
  /// На сколько умножается выпавшее значение.
  /// </summary>
  public const int RollStep = MaxRoll / DieSides;

  /// <summary>
  /// Проверка успешна: эффект карточки применяется.
  /// </summary>
  public bool Succeeded => Roll >= Chance;

  /// <summary>
  /// Провести проверку.
  /// </summary>
  /// <param name="cell">Ячейка карточки.</param>
  /// <param name="chance">Успешность персонажа.</param>
  /// <param name="random">Источник случайности.</param>
  public static SuccessCheck Perform(Cell cell, int chance, Random random) =>
    new(cell, chance, random.Next(1, DieSides + 1) * RollStep);
}
