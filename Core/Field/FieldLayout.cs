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

  /// <summary>
  /// Общая зона всех игроков: на всю ширину, нижний ряд — 5-й снизу поля.
  /// Между ней и зоной игрока остаётся один ряд — там подписаны итоги теорий.
  /// </summary>
  public static Zone CommonZone { get; } =
    new(0, Rows - CommonZoneBottomRowFromBottom - CommonZoneHeight + 1, Columns, CommonZoneHeight);

  /// <summary>
  /// Рабочая зона игрока: в самом низу поля, по центру.
  /// </summary>
  public static Zone PlayerZone { get; } =
    new((Columns - PlayerZoneWidth) / 2, Rows - PlayerZoneHeight, PlayerZoneWidth, PlayerZoneHeight);
}
