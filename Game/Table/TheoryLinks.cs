using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Game.Cards;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Связи между лидерами теорий цветом теории, под карточками.
/// <list type="bullet">
/// <item>Вертикальная — между соседними карточками стопки: линия выходит из нижней карточки вбок (сторона выбирается
/// случайно, но не меняется между кадрами), поднимается и входит в следующую карточку.</item>
/// <item>Горизонтальная — между соседними карточками семейства в ряду дочерних теорий: родительской и её побочными теориями.</item>
/// </list>
/// </summary>
public partial class TheoryLinks : Node2D
{
  private const float LineWidth = 3f;
  private const float CornerRadius = 8f;
  private const int CornerSegments = 6;

  /// <summary>
  /// На сколько линия вертикальной связи отходит от края карточки.
  /// </summary>
  private const float SideOffset = 18f;

  /// <summary>
  /// На сколько линия заходит под карточку, чтобы не было зазора у края.
  /// </summary>
  private const float Overlap = 6f;

  private static readonly Color NeutralColor = new("d8e6dc");

  private static readonly float HalfWidth = CardPainter.CardSize.X / 2;

  /// <summary>
  /// Высота точки связи над центром карточки: середина её видимой (верхней) половины.
  /// </summary>
  private static readonly float LinkRise = CardPainter.CardSize.Y / 4;

  /// <summary>
  /// Сессия игры: поле, на котором стоят стопки теорий.
  /// </summary>
  public GameSession Session { get; init; } = null!;

  /// <summary>
  /// Стол: где лежат ячейки и как они повёрнуты.
  /// </summary>
  public Board Board { get; init; } = null!;

  public override void _Process(double delta) => QueueRedraw();

  public override void _Draw()
  {
    var field = Session.Field;
    var horizontal = HorizontalLinks(field);
    // Карточки, у которых есть горизонтальная связь, и в какую сторону (+1 — вправо).
    var sides = new HashSet<(Cell Cell, int Side)>();
    foreach (var (left, right, _) in horizontal)
    {
      sides.Add((left, 1));
      sides.Add((right, -1));
    }

    foreach (var column in field.TheoryColumns)
    {
      var color = ColorOf(column);
      var cells = column.Cells;
      for (var i = 0; i + 1 < cells.Count; i++)
      {
        var side = SideOf(column, i);
        // Не ведём вертикальную связь туда, где уже идёт горизонтальная.
        if (sides.Contains((cells[i], side)) || sides.Contains((cells[i + 1], side)))
          side = -side;
        DrawVertical(cells[i].ToVector2I(), cells[i + 1].ToVector2I(), side, color);
      }
    }

    foreach (var (left, right, color) in horizontal)
      DrawHorizontal(left.ToVector2I(), right.ToVector2I(), color);

    DrawSetTransformMatrix(Transform2D.Identity);
  }

  /// <summary>
  /// Пары соседних карточек семейства в ряду дочерних теорий, слева направо.
  /// </summary>
  private List<(Cell Left, Cell Right, Color Color)> HorizontalLinks(PlayingField field)
  {
    var links = new List<(Cell Left, Cell Right, Color Color)>();
    var row = Board.SideTheoryRow;
    foreach (var parent in field.TheoryColumns.Where(column => column.IsHeadquarters))
    {
      var family = field.TheoryColumns.Where(column => column == parent || column.Theory.Parent == parent.Theory);
      var cells = family
        .SelectMany(column => column.Cells.Where(cell => cell.Row == row))
        .OrderBy(cell => cell.Column)
        .ToList();
      for (var i = 0; i + 1 < cells.Count; i++)
      {
        if (cells[i + 1].Column - cells[i].Column == 1)
          links.Add((cells[i], cells[i + 1], ColorOf(parent)));
      }
    }

    return links;
  }

  /// <summary>
  /// Вертикальная связь: из бока нижней карточки вверх и в бок верхней. Рисуется в координатах нижней карточки.
  /// </summary>
  /// <param name="side">+1 — справа, −1 — слева.</param>
  private void DrawVertical(Vector2I from, Vector2I to, int side, Color color)
  {
    var transform = Board.SlotTransform(from);
    var next = transform.AffineInverse() * Board.CardCenter(to);
    var inner = side * (HalfWidth - Overlap);
    var outer = side * (HalfWidth + SideOffset);
    var bottom = -LinkRise;
    var top = next.Y - LinkRise;

    DrawSetTransformMatrix(transform);
    DrawRounded([new(inner, bottom), new(outer, bottom), new(next.X + outer, top), new(next.X + inner, top)], color);
  }

  /// <summary>
  /// Горизонтальная связь: прямая между боками двух соседних карточек.
  /// </summary>
  private void DrawHorizontal(Vector2I left, Vector2I right, Color color)
  {
    var start = Board.SlotTransform(left) * new Vector2(HalfWidth - Overlap, -LinkRise);
    var end = Board.SlotTransform(right) * new Vector2(-HalfWidth + Overlap, -LinkRise);
    DrawSetTransformMatrix(Transform2D.Identity);
    DrawLine(start, end, color, LineWidth, antialiased: true);
  }

  /// <summary>
  /// Ломаная с немного скруглёнными углами.
  /// </summary>
  private void DrawRounded(Vector2[] path, Color color)
  {
    var points = new List<Vector2> { path[0] };
    for (var i = 1; i + 1 < path.Length; i++)
    {
      var corner = path[i];
      var before = path[i - 1];
      var after = path[i + 1];
      var radius = Mathf.Min(CornerRadius, Mathf.Min(corner.DistanceTo(before), corner.DistanceTo(after)) / 2);
      var start = corner + (before - corner).Normalized() * radius;
      var end = corner + (after - corner).Normalized() * radius;
      for (var s = 0; s <= CornerSegments; s++)
      {
        var t = (float)s / CornerSegments;
        // Квадратичная кривая Безье с вершиной в углу.
        points.Add(start.Lerp(corner, t).Lerp(corner.Lerp(end, t), t));
      }
    }

    points.Add(path[^1]);
    DrawPolyline([.. points], color, LineWidth, antialiased: true);
  }

  /// <summary>
  /// Сторона вертикальной связи между карточками <paramref name="index"/> и следующей: случайная, но постоянная
  /// для этой теории и этой пары карточек.
  /// </summary>
  private static int SideOf(TheoryColumn column, int index)
  {
    var hash = 17;
    foreach (var symbol in column.Theory.Name)
      hash = unchecked(hash * 31 + symbol);
    return new Random(unchecked(hash * 397 + index)).Next(2) == 0 ? -1 : 1;
  }

  private static Color ColorOf(TheoryColumn column) => TheoryHighlights.ColorOf(column.Theory) ?? NeutralColor;
}
