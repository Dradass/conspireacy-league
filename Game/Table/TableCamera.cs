using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Камера над столом: перемещение правой или средней кнопкой мыши, масштаб колесом.
/// </summary>
public partial class TableCamera : Camera2D
{
  private const float MinZoom = 0.15f;
  private const float MaxZoom = 2f;
  private const float ZoomStep = 1.1f;

  /// <summary>
  /// Область, за которую не выходит центр камеры.
  /// </summary>
  public Rect2 Bounds { get; set; }

  public override void _UnhandledInput(InputEvent @event)
  {
    switch (@event)
    {
      case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp } wheel:
        ZoomAt(wheel.Position, ZoomStep);
        break;
      case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown } wheel:
        ZoomAt(wheel.Position, 1 / ZoomStep);
        break;
      case InputEventMouseMotion motion
        when (motion.ButtonMask & (MouseButtonMask.Right | MouseButtonMask.Middle)) != 0:
        MoveTo(Position - motion.Relative / Zoom);
        break;
      default:
        return;
    }

    GetViewport().SetInputAsHandled();
  }

  private void ZoomAt(Vector2 screenPoint, float factor)
  {
    var offsetFromCenter = screenPoint - GetViewportRect().Size / 2;
    var worldPoint = Position + offsetFromCenter / Zoom;
    Zoom = Vector2.One * Mathf.Clamp(Zoom.X * factor, MinZoom, MaxZoom);
    // Точка под курсором остаётся на месте.
    MoveTo(worldPoint - offsetFromCenter / Zoom);
  }

  private void MoveTo(Vector2 position) => Position = position.Clamp(Bounds.Position, Bounds.End);
}
