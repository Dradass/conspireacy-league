using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Theories;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Подсветка стопок отмеченных теорий и их подтеорий поверх карточек. По умолчанию цвет полупрозрачен (75% прозрачности),
/// а у действующей теории с хотя бы тремя лидерами — плотнее (50% прозрачности).
/// </summary>
public partial class TheoryHighlights : Node2D
{
  private const float FadedAlpha = 0.25f;
  private const float ActiveAlpha = 0.5f;
  private const float CellInset = 4f;
  private const float DotsPerCell = 12;
  private const float DotMinRadius = 1.5f;
  private const float DotRadiusRange = 3.5f;
  private const float SideWidth = 6f;

  /// <summary>
  /// Как рисуется подсветка поверх ячейки помимо цвета.
  /// </summary>
  private enum Pattern
  {
    Plain,
    WhiteDots,
    BlackSides,
  }

  private sealed record Style(Color Color, Pattern Pattern = Pattern.Plain);

  private static readonly Dictionary<Theory, Style> Styles = CreateStyles();

  /// <summary>
  /// Цвет подсветки теории на общем поле (он же её фон) или <c>null</c>, если для теории цвет не задан.
  /// </summary>
  public static Color? ColorOf(Theory theory) => Styles.TryGetValue(theory, out var style) ? style.Color : null;

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
    foreach (var column in Session.Field.TheoryColumns)
    {
      if (!Styles.TryGetValue(column.Theory, out var style))
        continue;

      var active = column.IsActive;
      var color = style.Color with { A = active ? ActiveAlpha : FadedAlpha };
      foreach (var cell in ColumnCells(column))
      {
        // Рисуем в координатах ячейки: на радиальном поле она повёрнута.
        DrawSetTransformMatrix(Board.SlotTransform(cell));
        var rect = CellRect();
        DrawRect(rect, color);
        switch (style.Pattern)
        {
          case Pattern.WhiteDots:
            DrawDots(rect, cell);
            break;
          case Pattern.BlackSides:
            DrawRect(new Rect2(rect.Position, new Vector2(SideWidth, rect.Size.Y)), Colors.Black);
            DrawRect(new Rect2(rect.End - new Vector2(SideWidth, rect.Size.Y), new Vector2(SideWidth, rect.Size.Y)), Colors.Black);
            break;
        }
      }
    }

    DrawSetTransformMatrix(Transform2D.Identity);
  }

  /// <summary>
  /// Ячейки стопки теории: её карточки и ячейка над верхней, куда ляжет следующая.
  /// </summary>
  private IEnumerable<Vector2I> ColumnCells(TheoryColumn column)
  {
    var zone = Session.Field.CommonZone;
    var x = column.TheoryCell.Column;
    for (var k = 0; k <= column.Members.Count; k++)
    {
      var y = column.TheoryCell.Row - k;
      if (y >= zone.Top)
        yield return new Vector2I(x, y);
    }
  }

  /// <summary>
  /// Белые точки случайного размера. Генератор задан ячейкой, поэтому точки не мерцают между кадрами.
  /// </summary>
  private void DrawDots(Rect2 rect, Vector2I cell)
  {
    var random = new Random(cell.X * 1000 + cell.Y);
    for (var i = 0; i < DotsPerCell; i++)
    {
      var center = rect.Position + new Vector2(random.NextSingle() * rect.Size.X, random.NextSingle() * rect.Size.Y);
      DrawCircle(center, DotMinRadius + random.NextSingle() * DotRadiusRange, Colors.White);
    }
  }

  /// <summary>
  /// Прямоугольник ячейки относительно её центра (см. <see cref="Board.SlotTransform"/>).
  /// </summary>
  private static Rect2 CellRect()
  {
    var inset = new Vector2(CellInset, CellInset);
    return new Rect2(-Board.CellSize / 2 + inset, Board.CellSize - inset * 2);
  }

  /// <summary>
  /// Цвета отмеченных теорий; подтеории получают цвет своей теории.
  /// </summary>
  private static Dictionary<Theory, Style> CreateStyles()
  {
    var styles = new Dictionary<Theory, Style>();

    void Add(Theory parent, Style style)
    {
      styles[parent] = style;
      foreach (var child in TheoryCatalog.ChildrenOf(parent))
        styles[child] = style;
    }

    Add(TheoryCatalog.AliensAmongUs, new Style(new Color("33cc44")));
    Add(TheoryCatalog.SecretGovernment, new Style(new Color("3a5573")));
    Add(TheoryCatalog.ScienceConspiracy, new Style(new Color("4a4a4a")));
    Add(TheoryCatalog.CommunistConspiracy, new Style(new Color("d62828")));
    Add(TheoryCatalog.GoldenBillion, new Style(new Color("e8c547")));
    Add(TheoryCatalog.CiaKillsPeople, new Style(new Color("2ea8ff")));
    Add(TheoryCatalog.LeninMushroom, new Style(new Color("ff6f61"), Pattern.WhiteDots));
    Add(TheoryCatalog.SatanistsKidnapChildren, new Style(new Color("8b0f1a"), Pattern.BlackSides));
    Add(TheoryCatalog.BirdsAreDrones, new Style(new Color("87ceeb")));
    Add(TheoryCatalog.TinFoilHat, new Style(new Color("e6e6e6")));
    return styles;
  }
}
