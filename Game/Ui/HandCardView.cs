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

  /// <summary>
  /// Отображаемая карточка.
  /// </summary>
  public Card Card { get; private set; } = null!;

  /// <summary>
  /// Центр карточки в координатах слоя интерфейса.
  /// </summary>
  public Vector2 Center => GetGlobalRect().GetCenter();

  /// <summary>
  /// Игрок начал тянуть карточку.
  /// </summary>
  public event Action<HandCardView>? DragStarted;

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
    if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
      return;

    AcceptEvent();
    DragStarted?.Invoke(this);
  }

  public override void _Draw() => CardPainter.DrawFace(this, new Rect2(Vector2.Zero, CardSize), Card);
}
