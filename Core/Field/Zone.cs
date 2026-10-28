namespace ConspiracyLeague.Core.Field;

/// <summary>
/// Прямоугольная зона поля.
/// </summary>
/// <param name="Left">Левый столбец.</param>
/// <param name="Top">Верхняя строка.</param>
/// <param name="Width">Ширина в ячейках.</param>
/// <param name="Height">Высота в ячейках.</param>
public sealed record Zone(int Left, int Top, int Width, int Height)
{
  /// <summary>
  /// Нижняя строка зоны.
  /// </summary>
  public int BottomRow => Top + Height - 1;

  /// <summary>
  /// Входит ли ячейка в зону.
  /// </summary>
  public bool Contains(Cell cell) =>
    cell.Column >= Left && cell.Column < Left + Width && cell.Row >= Top && cell.Row <= BottomRow;
}
