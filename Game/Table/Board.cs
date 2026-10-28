using ConspiracyLeague.Game.Cards;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Рабочая поверхность: сетка ячеек, в каждую из которых можно положить одну карточку.
/// </summary>
/// <remarks>
/// Стол должен стоять в начале координат: локальные координаты стола совпадают с мировыми.
/// </remarks>
public partial class Board : Node2D
{
  /// <summary>
  /// Количество столбцов сетки.
  /// </summary>
  public const int Columns = 50;

  /// <summary>
  /// Количество строк сетки.
  /// </summary>
  public const int Rows = 50;

  /// <summary>
  /// Размер ячейки: карточка плюс поля вокруг неё.
  /// </summary>
  public static readonly Vector2 CellSize = CardPainter.CardSize + new Vector2(20, 20);

  private const float SlotInset = 4f;
  private const double SnapDuration = 0.12;

  private static readonly Color TableColor = new("2f4a3a");
  private static readonly Color SlotColor = new("37574a");
  private static readonly Color FreeHighlightColor = new(0.55f, 0.95f, 0.6f, 0.35f);
  private static readonly Color BusyHighlightColor = new(1f, 0.35f, 0.3f, 0.35f);

  private readonly Dictionary<Vector2I, CardView> _cards = new();
  private ColorRect _highlight = null!;
  private Node2D _cardsLayer = null!;

  /// <summary>
  /// Размер стола в пикселях.
  /// </summary>
  public static Vector2 Size => new(Columns * CellSize.X, Rows * CellSize.Y);

  public override void _Ready()
  {
    _highlight = new ColorRect
    {
      Name = "Highlight",
      Size = CellSize,
      Visible = false,
      MouseFilter = Control.MouseFilterEnum.Ignore,
    };
    AddChild(_highlight);

    _cardsLayer = new Node2D { Name = "Cards" };
    AddChild(_cardsLayer);
  }

  public override void _Draw()
  {
    DrawRect(new Rect2(Vector2.Zero, Size), TableColor);
    var slotSize = CellSize - new Vector2(SlotInset, SlotInset) * 2;
    for (var x = 0; x < Columns; x++)
    {
      for (var y = 0; y < Rows; y++)
        DrawRect(new Rect2(CellOrigin(new Vector2I(x, y)) + new Vector2(SlotInset, SlotInset), slotSize), SlotColor);
    }
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
    return cell.X is >= 0 and < Columns && cell.Y is >= 0 and < Rows ? cell : null;
  }

  /// <summary>
  /// Центр ячейки.
  /// </summary>
  public static Vector2 CellCenter(Vector2I cell) => CellOrigin(cell) + CellSize / 2;

  /// <summary>
  /// Свободна ли ячейка.
  /// </summary>
  public bool IsFree(Vector2I cell) => !_cards.ContainsKey(cell);

  /// <summary>
  /// Положить карточку в свободную ячейку. Карточка плавно доезжает до центра ячейки.
  /// </summary>
  public void Place(CardView view, Vector2I cell)
  {
    if (!IsFree(cell))
      throw new InvalidOperationException($"Ячейка {cell} уже занята.");

    _cards[cell] = view;
    view.Lifted = false;
    _cardsLayer.AddChild(view);
    view.CreateTween()
      .TweenProperty(view, Node2D.PropertyName.Position.ToString(), CellCenter(cell), SnapDuration)
      .SetTrans(Tween.TransitionType.Cubic)
      .SetEase(Tween.EaseType.Out);
  }

  /// <summary>
  /// Забрать карточку из ячейки.
  /// </summary>
  /// <returns>Карточка или <c>null</c>, если ячейка пуста.</returns>
  public CardView? Take(Vector2I cell)
  {
    if (!_cards.Remove(cell, out var view))
      return null;

    _cardsLayer.RemoveChild(view);
    return view;
  }

  /// <summary>
  /// Подсветить ячейку, куда будет положена карточка.
  /// </summary>
  /// <param name="cell">Ячейка или <c>null</c>, чтобы убрать подсветку.</param>
  public void Highlight(Vector2I? cell)
  {
    _highlight.Visible = cell is not null;
    if (cell is not { } target)
      return;

    _highlight.Position = CellOrigin(target);
    _highlight.Color = IsFree(target) ? FreeHighlightColor : BusyHighlightColor;
  }

  private static Vector2 CellOrigin(Vector2I cell) => new(cell.X * CellSize.X, cell.Y * CellSize.Y);
}
