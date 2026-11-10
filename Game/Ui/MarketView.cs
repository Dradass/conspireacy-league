using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Game.Cards;
using ConspiracyLeague.Game.Table;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Окно рынка: уменьшенная копия рынка на столе — колода и три строки открытых карточек.
/// Карточку отсюда можно перетащить в руку (купить), правой кнопкой — посмотреть описание,
/// двойным щелчком — перейти к рынку на столе.
/// </summary>
public partial class MarketView : VBoxContainer
{
  private const float CardScale = 0.385f;
  private const float Gap = 4f;
  private const float DeckGap = 14f;
  private const int Rows = 3;
  private const int TitleFontSize = 14;

  private static readonly Vector2 CellSize = CardPainter.CardSize * CardScale;
  private static readonly Color EmptyCellColor = UiStyle.Glow with { A = 0.18f };

  private Label _title = null!;
  private Control _grid = null!;
  private int _columns;
  private int _signature;

  /// <summary>
  /// Сессия игры.
  /// </summary>
  public GameSession Session { get; init; } = null!;

  /// <summary>
  /// Связь окон рынка с контроллерами.
  /// </summary>
  public MarketHub Hub { get; init; } = null!;

  private Market Market => Session.Market;

  /// <summary>
  /// Левый столбец открытых карточек (в координатах поля).
  /// </summary>
  private int FirstColumn => Market.Zone.Left + Market.FirstOpenColumn;

  public override void _Ready()
  {
    MouseFilter = MouseFilterEnum.Ignore;
    AddThemeConstantOverride("separation", 4);

    _title = new Label { MouseFilter = MouseFilterEnum.Ignore };
    _title.AddThemeFontSizeOverride("font_size", TitleFontSize);
    AddChild(_title);

    _grid = new Control
    {
      Name = "Grid",
      MouseFilter = MouseFilterEnum.Stop,
      MouseDefaultCursorShape = CursorShape.PointingHand,
      TooltipText = "Перетащите карточку в руку, чтобы купить её. Правая кнопка — описание, двойной щелчок — рынок на столе.",
    };
    _grid.Draw += DrawGrid;
    _grid.GuiInput += OnGridInput;
    AddChild(_grid);

    Refresh();
  }

  /// <summary>
  /// Рынок меняется от событий рынка и перетаскивания; дешевле сверять его состояние каждый кадр.
  /// </summary>
  public override void _Process(double delta)
  {
    var signature = Signature();
    if (signature != _signature)
      Refresh();
  }

  private void Refresh()
  {
    _signature = Signature();
    _title.Text = $"Рынок · в колоде {Market.DeckCount}";
    // Обычно в строке столько мест, сколько открыто на старте (2 на игрока); дополнительные карточки могут лечь правее.
    var lastColumn = Market.OpenCards.Keys.Select(cell => cell.Column).DefaultIfEmpty(FirstColumn).Max();
    _columns = Math.Max(Session.MarketCardsPerRow, lastColumn - FirstColumn + 1);
    _grid.CustomMinimumSize = new Vector2(
      CellSize.X + DeckGap + _columns * CellSize.X + (_columns - 1) * Gap,
      Rows * CellSize.Y + (Rows - 1) * Gap);
    _grid.QueueRedraw();
  }

  private int Signature()
  {
    var hash = new HashCode();
    hash.Add(Market.DeckCount);
    hash.Add(Hub.DraggedCell);
    foreach (var (cell, card) in Market.OpenCards.OrderBy(pair => pair.Key.Row).ThenBy(pair => pair.Key.Column))
    {
      hash.Add(cell);
      hash.Add(card);
    }

    return hash.ToHashCode();
  }

  private Rect2 SlotRect(int column, int row) => new(
    new Vector2(CellSize.X + DeckGap + (column - FirstColumn) * (CellSize.X + Gap), (row - Market.Zone.Top) * (CellSize.Y + Gap)),
    CellSize);

  private Rect2 DeckRect => new(new Vector2(0, (Market.DeckCell.Row - Market.Zone.Top) * (CellSize.Y + Gap)), CellSize);

  private void DrawGrid()
  {
    if (Market.DeckCount > 0)
      CardPainter.DrawBack(_grid, DeckRect, $"Рынок\n{Market.DeckCount}");
    else
      _grid.DrawRect(DeckRect, EmptyCellColor, filled: false);

    for (var row = Market.Zone.Top; row < Market.Zone.Top + Rows; row++)
    {
      for (var column = FirstColumn; column < FirstColumn + _columns; column++)
      {
        var rect = SlotRect(column, row);
        var cell = new Cell(column, row);
        if (Market.OpenCards.TryGetValue(cell, out var card) && Hub.DraggedCell != cell.ToVector2I())
          CardPainter.DrawFace(_grid, rect, card);
        // Средняя строка — только для дополнительных карточек: пустые места в ней не рисуем, чтобы не путать.
        else if (row != Market.DeckCell.Row)
          _grid.DrawRect(rect, EmptyCellColor, filled: false);
      }
    }
  }

  /// <summary>
  /// Ячейка рынка с открытой карточкой под точкой сетки или <c>null</c>.
  /// </summary>
  private Cell? CardCellAt(Vector2 local)
  {
    for (var row = Market.Zone.Top; row < Market.Zone.Top + Rows; row++)
    {
      for (var column = FirstColumn; column < FirstColumn + _columns; column++)
      {
        var cell = new Cell(column, row);
        if (SlotRect(column, row).HasPoint(local) && Market.OpenCards.ContainsKey(cell))
          return cell;
      }
    }

    return null;
  }

  private void OnGridInput(InputEvent @event)
  {
    switch (@event)
    {
      case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left, DoubleClick: true }:
        _grid.AcceptEvent();
        Hub.RaiseFocus();
        break;
      case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } press when CardCellAt(press.Position) is { } cell:
        _grid.AcceptEvent();
        Hub.RaiseDragStarted(cell.ToVector2I());
        break;
      case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } press when CardCellAt(press.Position) is { } cell:
        _grid.AcceptEvent();
        Hub.RaiseInfoPressed(Market.OpenCards[cell]);
        break;
      case InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Right }:
        _grid.AcceptEvent();
        Hub.RaiseInfoReleased();
        break;
    }
  }
}
