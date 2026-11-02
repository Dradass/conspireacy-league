using ConspiracyLeague.Game.Cards;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Жетон первого игрока: золотой кружок с «1», лежит на углу зоны игрока, который начинает раунд.
/// Начало координат узла — центр жетона.
/// </summary>
public partial class FirstPlayerToken : Node2D
{
  /// <summary>
  /// Радиус жетона.
  /// </summary>
  public const float Radius = 30f;

  private const float RimWidth = 4f;
  private const int FontSize = 30;
  private const double MoveDuration = 0.6;

  private static readonly Color FaceColor = new("e0b84f");
  private static readonly Color RimColor = new("6b4f12");
  private static readonly Color InkColor = new("2a1d05");

  private Tween? _move;

  /// <summary>
  /// Переложить жетон: сразу или с анимацией.
  /// </summary>
  public void MoveTo(Vector2 position, bool animate)
  {
    _move?.Kill();
    if (!animate)
    {
      Position = position;
      return;
    }

    _move = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
    _move.TweenProperty(this, "position", position, MoveDuration);
  }

  public override void _Draw()
  {
    DrawCircle(Vector2.Zero, Radius, FaceColor);
    DrawArc(Vector2.Zero, Radius - RimWidth / 2, 0, Mathf.Tau, 48, RimColor, RimWidth, antialiased: true);
    var area = new Rect2(-Radius, -Radius, Radius * 2, Radius * 2);
    CardPainter.DrawCenteredText(this, "1", area, FontSize, InkColor);
  }
}
