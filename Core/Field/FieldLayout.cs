namespace ConspiracyLeague.Core.Field;

/// <summary>
/// Размеры поля и расположение зон.
/// </summary>
public static class FieldLayout
{
  /// <summary>
  /// Количество столбцов поля.
  /// </summary>
  public const int Columns = 50;

  /// <summary>
  /// Количество строк поля.
  /// </summary>
  public const int Rows = 50;

  private const int CommonZoneHeight = 9;
  private const int CommonZoneBottomRowFromBottom = 5;
  private const int PlayerZoneWidth = 7;
  private const int PlayerZoneHeight = 3;
  private const int PlayerZoneGap = 3;

  /// <summary>
  /// Сколько рабочих зон игроков помещается в ряд по ширине поля.
  /// </summary>
  public const int MaxPlayers = (Columns + PlayerZoneGap) / (PlayerZoneWidth + PlayerZoneGap);

  /// <summary>
  /// Общая зона всех игроков: на всю ширину, нижний ряд — 5-й снизу поля.
  /// Между ней и зоной игрока остаётся один ряд — там подписаны итоги теорий.
  /// </summary>
  public static Zone CommonZone { get; } =
    new(0, Rows - CommonZoneBottomRowFromBottom - CommonZoneHeight + 1, Columns, CommonZoneHeight);

  /// <summary>
  /// Рабочая зона (личный штаб) игрока: в самом низу поля. Зоны всех игроков стоят в ряд по центру,
  /// между соседними зонами — пустые столбцы (<see cref="PlayerZoneGap"/>).
  /// </summary>
  /// <param name="index">Номер игрока, с нуля, слева направо.</param>
  /// <param name="playerCount">Количество игроков.</param>
  public static Zone PlayerZone(int index, int playerCount)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(playerCount);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(playerCount, MaxPlayers);
    ArgumentOutOfRangeException.ThrowIfNegative(index);
    ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, playerCount);

    var rowWidth = playerCount * PlayerZoneWidth + (playerCount - 1) * PlayerZoneGap;
    var left = (Columns - rowWidth) / 2 + index * (PlayerZoneWidth + PlayerZoneGap);
    return new Zone(left, Rows - PlayerZoneHeight, PlayerZoneWidth, PlayerZoneHeight);
  }
}
