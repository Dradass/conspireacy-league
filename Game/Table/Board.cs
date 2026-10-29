using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Game.Cards;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Рабочая поверхность: сетка ячеек с зонами игроков и общей зоной.
/// Правила выкладывания — в <see cref="PlayingField"/>, стол их только отображает.
/// </summary>
/// <remarks>
/// Стол должен стоять в начале координат: локальные координаты стола совпадают с мировыми.
/// </remarks>
public partial class Board : Node2D
{
  /// <summary>
  /// Размер ячейки: карточка плюс поля вокруг неё.
  /// </summary>
  public static readonly Vector2 CellSize = CardPainter.CardSize + new Vector2(20, 20);

  private const float SlotInset = 4f;
  private const float ZoneBorderWidth = 6f;
  private const double SnapDuration = 0.12;
  private const float AvailableFillAlpha = 0.18f;
  private const float AvailableBorderAlpha = 0.8f;
  private const float AvailableBorderWidth = 3f;
  private const int TotalsFontSize = 14;
  private const int TotalsValueFontSize = 28;

  private static readonly Color TableColor = new("26342c");
  private static readonly Color SlotColor = new("2c3d33");
  private static readonly Color CommonZoneColor = new("2f4a3a");
  private static readonly Color CommonSlotColor = new("37574a");
  private static readonly Color CommonBorderColor = new("6f9c80");
  private static readonly Color OwnZoneColor = new("5a4a26");
  private static readonly Color OwnSlotColor = new("6e5c33");
  private static readonly Color OwnBorderColor = new("e0b84f");
  private static readonly Color OtherZoneColor = new("3a3550");
  private static readonly Color OtherSlotColor = new("48416a");
  private static readonly Color OtherBorderColor = new("9a8fd0");
  private static readonly Color AllowedColor = new(0.55f, 0.95f, 0.6f, 0.35f);
  private static readonly Color PaidColor = new(1f, 0.85f, 0.3f, 0.4f);
  private static readonly Color DeniedColor = new(1f, 0.35f, 0.3f, 0.35f);
  private static readonly Color TotalsColor = new("d8e6dc");

  private readonly Dictionary<Vector2I, PlacementCheck> _available = new();
  private readonly Dictionary<Vector2I, CardView> _views = new();
  private readonly Dictionary<TheoryColumn, AdeptsView> _adepts = new();
  private ColorRect _highlight = null!;
  private Label _highlightCost = null!;
  private Node2D _cardsLayer = null!;
  private Node2D _adeptsLayer = null!;

  /// <summary>
  /// Поле, которое отображает стол.
  /// </summary>
  public PlayingField Field { get; init; } = null!;

  /// <summary>
  /// Игрок, за которого играют на этом экране: его зона выделена своим цветом.
  /// </summary>
  public Player LocalPlayer { get; init; } = null!;

  /// <summary>
  /// Размер стола в пикселях.
  /// </summary>
  public Vector2 Size => new(Field.Columns * CellSize.X, Field.Rows * CellSize.Y);

  public override void _Ready()
  {
    _highlight = new ColorRect
    {
      Name = "Highlight",
      Size = CellSize,
      Visible = false,
      MouseFilter = Control.MouseFilterEnum.Ignore,
    };
    _highlightCost = new Label
    {
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Bottom,
      Size = CellSize,
      MouseFilter = Control.MouseFilterEnum.Ignore,
    };
    _highlightCost.AddThemeFontSizeOverride("font_size", 18);
    _highlightCost.AddThemeColorOverride("font_color", OwnBorderColor);
    _highlightCost.AddThemeColorOverride("font_outline_color", Colors.Black);
    _highlightCost.AddThemeConstantOverride("outline_size", 6);
    _highlight.AddChild(_highlightCost);
    AddChild(_highlight);

    _cardsLayer = new Node2D { Name = "Cards" };
    AddChild(_cardsLayer);
    _adeptsLayer = new Node2D { Name = "Adepts" };
    AddChild(_adeptsLayer);

    Field.AdeptsAdded += OnAdeptsAdded;
  }

  public override void _ExitTree() => Field.AdeptsAdded -= OnAdeptsAdded;

  public override void _Draw()
  {
    DrawRect(new Rect2(Vector2.Zero, Size), TableColor);
    DrawZone(Field.CommonZone, CommonZoneColor, CommonBorderColor);
    foreach (var (player, zone) in Field.PlayerZones)
    {
      var own = player == LocalPlayer;
      DrawZone(zone, own ? OwnZoneColor : OtherZoneColor, own ? OwnBorderColor : OtherBorderColor);
    }

    var slotSize = CellSize - new Vector2(SlotInset, SlotInset) * 2;
    for (var x = 0; x < Field.Columns; x++)
    {
      for (var y = 0; y < Field.Rows; y++)
      {
        var cell = new Vector2I(x, y);
        DrawRect(new Rect2(CellOrigin(cell) + new Vector2(SlotInset, SlotInset), slotSize), SlotColorOf(cell.ToCell()));
      }
    }

    DrawTheoryTotals();

    foreach (var (cell, check) in _available)
    {
      var slot = new Rect2(CellOrigin(cell) + new Vector2(SlotInset, SlotInset), slotSize);
      var color = check.Cost > 0 ? PaidColor : AllowedColor;
      DrawRect(slot, color with { A = AvailableFillAlpha });
      DrawRect(slot.Grow(-AvailableBorderWidth / 2), color with { A = AvailableBorderAlpha }, filled: false, width: AvailableBorderWidth);
    }
  }

  /// <summary>
  /// Подсветить все ячейки, куда можно положить взятую карточку.
  /// </summary>
  public void ShowAvailable(IEnumerable<(Cell Cell, PlacementCheck Check)> cells)
  {
    _available.Clear();
    foreach (var (cell, check) in cells)
      _available[new Vector2I(cell.Column, cell.Row)] = check;
    QueueRedraw();
  }

  /// <summary>
  /// Подсветить ячейки, которые можно выбрать целью действия.
  /// </summary>
  public void ShowTargets(IEnumerable<Cell> cells) => ShowAvailable(cells.Select(cell => (cell, PlacementCheck.Allowed)));

  /// <summary>
  /// Убрать подсветку доступных ячеек.
  /// </summary>
  public void HideAvailable()
  {
    _available.Clear();
    QueueRedraw();
  }

  /// <summary>
  /// Ячейка под точкой стола.
  /// </summary>
  /// <param name="point">Точка в мировых координатах.</param>
  /// <returns>Ячейка или <c>null</c>, если точка вне сетки.</returns>
  public Vector2I? CellAt(Vector2 point)
  {
    var local = ToLocal(point);
    var cell = new Vector2I(Mathf.FloorToInt(local.X / CellSize.X), Mathf.FloorToInt(local.Y / CellSize.Y));
    return cell.X >= 0 && cell.X < Field.Columns && cell.Y >= 0 && cell.Y < Field.Rows ? cell : null;
  }

  /// <summary>
  /// Карточка, лежащая в ячейке.
  /// </summary>
  public CardView? ViewAt(Vector2I cell) => _views.GetValueOrDefault(cell);

  /// <summary>
  /// Центр ячейки.
  /// </summary>
  public static Vector2 CellCenter(Vector2I cell) => CellOrigin(cell) + CellSize / 2;

  /// <summary>
  /// Центр зоны.
  /// </summary>
  public static Vector2 ZoneCenter(Zone zone) => ZoneRect(zone).GetCenter();

  /// <summary>
  /// Положить карточку в ячейку. Карточка плавно доезжает до центра ячейки.
  /// </summary>
  /// <remarks>
  /// Проверять правила и класть карточку в <see cref="Field"/> нужно до вызова.
  /// </remarks>
  public void Place(CardView view, Vector2I cell)
  {
    view.Lifted = false;
    _cardsLayer.AddChild(view);
    _views[cell] = view;
    // Перерисовываем итоги теорий под общей зоной.
    QueueRedraw();
    view.CreateTween()
      .TweenProperty(view, Node2D.PropertyName.Position.ToString(), CellCenter(cell), SnapDuration)
      .SetTrans(Tween.TransitionType.Cubic)
      .SetEase(Tween.EaseType.Out);
  }

  /// <summary>
  /// Подсветить ячейку, куда будет положена карточка.
  /// </summary>
  /// <param name="cell">Ячейка или <c>null</c>, чтобы убрать подсветку.</param>
  /// <param name="check">Можно ли положить туда карточку.</param>
  public void Highlight(Vector2I? cell, PlacementCheck? check = null)
  {
    _highlight.Visible = cell is not null;
    if (cell is not { } target)
      return;

    var cost = check is { IsAllowed: true } ? check.Cost : 0;
    _highlight.Position = CellOrigin(target);
    _highlight.Color = check is not { IsAllowed: true } ? DeniedColor : cost > 0 ? PaidColor : AllowedColor;
    _highlightCost.Text = cost > 0 ? $"−{cost}$" : string.Empty;
  }

  private void OnAdeptsAdded(TheoryColumn column, int count)
  {
    if (!_adepts.TryGetValue(column, out var view))
    {
      view = new AdeptsView { Name = $"Adepts{column.Theory.Name}", Position = CellCenter(column.TheoryCell.ToVector2I()) };
      _adeptsLayer.AddChild(view);
      _adepts[column] = view;
    }

    view.Add(count);
    // Перерисовываем итоги теорий: под теорией подписано количество адептов.
    QueueRedraw();
  }

  /// <summary>
  /// Под каждым столбцом теории, в ряду под общей зоной, — суммарное влияние, количество человек и адептов.
  /// </summary>
  private void DrawTheoryTotals()
  {
    var row = Field.CommonZone.BottomRow + 1;
    if (row >= Field.Rows)
      return;

    // В одном столбце могут оказаться две теории (дочерняя над чужой родительской) — тогда подписываем названия.
    foreach (var group in Field.TheoryColumns.GroupBy(column => column.Cells[0].Column))
    {
      var area = new Rect2(CellOrigin(new Vector2I(group.Key, row)), CellSize).Grow(-SlotInset);
      if (group.Count() == 1)
      {
        DrawTotals(group.Single(), area);
        continue;
      }

      var text = string.Join("\n", group.Select(column =>
        $"{column.Theory}:\nвлияние {column.Influence}, человек {column.Cards.Count}, адептов {column.Adepts}"));
      CardPainter.DrawCenteredText(this, text, area, TotalsFontSize, TotalsColor);
    }
  }

  private void DrawTotals(TheoryColumn column, Rect2 area)
  {
    (string Text, int FontSize)[] lines =
    [
      ("Влияние", TotalsFontSize),
      (column.Influence.ToString(), TotalsValueFontSize),
      ("Человек", TotalsFontSize),
      (column.Cards.Count.ToString(), TotalsValueFontSize),
      ("Адептов", TotalsFontSize),
      (column.Adepts.ToString(), TotalsValueFontSize),
    ];
    // Высота строки пропорциональна размеру шрифта.
    var scale = area.Size.Y / lines.Sum(line => line.FontSize);
    var top = area.Position.Y;
    foreach (var (text, fontSize) in lines)
    {
      var lineArea = new Rect2(area.Position.X, top, area.Size.X, fontSize * scale);
      CardPainter.DrawCenteredText(this, text, lineArea, fontSize, TotalsColor);
      top = lineArea.End.Y;
    }
  }

  private void DrawZone(Zone zone, Color fill, Color border)
  {
    var rect = ZoneRect(zone);
    DrawRect(rect, fill);
    DrawRect(rect.Grow(-ZoneBorderWidth / 2), border, filled: false, width: ZoneBorderWidth);
  }

  private Color SlotColorOf(Cell cell)
  {
    if (Field.CommonZone.Contains(cell))
      return CommonSlotColor;

    foreach (var (player, zone) in Field.PlayerZones)
    {
      if (zone.Contains(cell))
        return player == LocalPlayer ? OwnSlotColor : OtherSlotColor;
    }

    return SlotColor;
  }

  private static Rect2 ZoneRect(Zone zone) =>
    new(CellOrigin(new Vector2I(zone.Left, zone.Top)), new Vector2(zone.Width * CellSize.X, zone.Height * CellSize.Y));

  private static Vector2 CellOrigin(Vector2I cell) => new(cell.X * CellSize.X, cell.Y * CellSize.Y);
}

/// <summary>
/// Перевод ячеек между Godot и Core.
/// </summary>
public static class CellExtensions
{
  /// <summary>
  /// Ячейка поля из ячейки стола.
  /// </summary>
  public static Cell ToCell(this Vector2I cell) => new(cell.X, cell.Y);

  /// <summary>
  /// Ячейка стола из ячейки поля.
  /// </summary>
  public static Vector2I ToVector2I(this Cell cell) => new(cell.Column, cell.Row);
}
