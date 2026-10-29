namespace ConspiracyLeague.Core.Field;

/// <summary>
/// Ячейка поля.
/// </summary>
/// <remarks>
/// Строки нумеруются сверху вниз, как на экране: «выше» — значит строка с меньшим номером.
/// </remarks>
/// <param name="Column">Столбец.</param>
/// <param name="Row">Строка.</param>
public readonly record struct Cell(int Column, int Row)
{
  /// <summary>
  /// Ячейка сверху.
  /// </summary>
  public Cell Above => this with { Row = Row - 1 };

  /// <summary>
  /// Ячейка снизу.
  /// </summary>
  public Cell Below => this with { Row = Row + 1 };

  /// <summary>
  /// Ячейка слева.
  /// </summary>
  public Cell Left => this with { Column = Column - 1 };

  /// <summary>
  /// Ячейка справа.
  /// </summary>
  public Cell Right => this with { Column = Column + 1 };

  /// <summary>
  /// Соседние ячейки по горизонтали и вертикали.
  /// </summary>
  public IEnumerable<Cell> Neighbors => [Above, Below, Left, Right];

  /// <summary>
  /// Соседние ячейки по горизонтали, вертикали и диагонали.
  /// </summary>
  public IEnumerable<Cell> Surrounding => [.. Neighbors, Above.Left, Above.Right, Below.Left, Below.Right];

  public override string ToString() => $"({Column}, {Row})";
}
