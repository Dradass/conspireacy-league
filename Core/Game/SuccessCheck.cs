using ConspiracyLeague.Core.Field;

namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Проверка успеха персонажа, которого играют из личного штаба: бросок двадцатигранного кубика.
/// </summary>
/// <remarks>
/// Каждая грань — <see cref="ChanceStep"/> процентов. Младшие грани — провал, остальные — успех, так что успех
/// выпадает с вероятностью успеха персонажа. Например, при вероятности успеха 90% провал — 1 или 2, успех — 3–20.
/// </remarks>
/// <param name="Cell">Ячейка карточки.</param>
/// <param name="Chance">Вероятность успеха персонажа в процентах (0–100).</param>
/// <param name="Die">Выпавшая грань кубика (1–20).</param>
public sealed record SuccessCheck(Cell Cell, int Chance, int Die)
{
  /// <summary>
  /// Наибольшая вероятность успеха в процентах.
  /// </summary>
  public const int MaxChance = 100;

  /// <summary>
  /// Сколько граней у кубика.
  /// </summary>
  public const int DieSides = 20;

  /// <summary>
  /// Сколько процентов вероятности приходится на одну грань.
  /// </summary>
  public const int ChanceStep = MaxChance / DieSides;

  /// <summary>
  /// Наименьшая грань, на которой проверка успешна. Больше <see cref="DieSides"/> — успех невозможен.
  /// </summary>
  public int FirstSuccessFace => (MaxChance - Chance) / ChanceStep + 1;

  /// <summary>
  /// Проверка успешна: эффект карточки применяется.
  /// </summary>
  public bool Succeeded => Die >= FirstSuccessFace;

  /// <summary>
  /// Провести проверку.
  /// </summary>
  /// <param name="cell">Ячейка карточки.</param>
  /// <param name="chance">Вероятность успеха персонажа.</param>
  /// <param name="random">Источник случайности.</param>
  public static SuccessCheck Perform(Cell cell, int chance, Random random) =>
    new(cell, chance, random.Next(1, DieSides + 1));
}
