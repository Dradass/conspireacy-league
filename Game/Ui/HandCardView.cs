using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Game.Cards;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Карточка в руке игрока. Её можно взять и перетащить на стол.
/// </summary>
public partial class HandCardView : Control
{
  /// <summary>
  /// Карточки в руке мельче, чем на столе: рука не должна закрывать зону игрока.
  /// </summary>
  public static readonly Vector2 CardSize = CardPainter.CardSize * 0.75f;

  private const float SelectedBorderWidth = 4f;

  private static readonly Color SelectedColor = new(0.55f, 0.95f, 0.6f);

  private bool _selected;

  /// <summary>
  /// Отображаемая карточка.
  /// </summary>
  public Card Card { get; private set; } = null!;

  /// <summary>
  /// Центр карточки в координатах слоя интерфейса.
  /// </summary>
  public Vector2 Center => GetGlobalRect().GetCenter();

  /// <summary>
  /// Карточка выбрана для действия: она обведена рамкой.
  /// </summary>
  public bool Selected
  {
    get => _selected;
    set
    {
      _selected = value;
      QueueRedraw();
    }
  }

  /// <summary>
  /// Игрок начал тянуть карточку.
  /// </summary>
  public event Action<HandCardView>? DragStarted;

  /// <summary>
  /// Правую кнопку нажали на карточке (отсчёт удержания для описания).
  /// </summary>
  public event Action<HandCardView>? InfoPressed;

  /// <summary>
  /// Правую кнопку отпустили.
  /// </summary>
  public event Action<HandCardView>? InfoReleased;

  /// <summary>
  /// Создать отображение карточки в руке.
  /// </summary>
  public static HandCardView Create(Card card) => new()
  {
    Card = card,
    Name = card.GetType().Name,
    CustomMinimumSize = CardSize,
    MouseFilter = MouseFilterEnum.Stop,
    MouseDefaultCursorShape = CursorShape.PointingHand,
  };

  public override void _GuiInput(InputEvent @event)
  {
    if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
    {
      AcceptEvent();
      InfoPressed?.Invoke(this);
      return;
    }
    if (@event is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Right })
    {
      AcceptEvent();
      InfoReleased?.Invoke(this);
      return;
    }

    if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
      return;

    AcceptEvent();
    DragStarted?.Invoke(this);
  }

  public override void _Draw()
  {
    var rect = new Rect2(Vector2.Zero, CardSize);
    CardPainter.DrawFace(this, rect, Card);
    if (_selected)
      DrawRect(rect.Grow(-SelectedBorderWidth / 2), SelectedColor, filled: false, width: SelectedBorderWidth);
  }
}
