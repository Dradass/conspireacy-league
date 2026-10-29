using ConspiracyLeague.Core.Cards;
using Godot;

namespace ConspiracyLeague.Game.Cards;

/// <summary>
/// Карточка на столе. Начало координат узла — центр карточки.
/// </summary>
public partial class CardView : Node2D
{
  private const double FlipDuration = 0.3;

  private static readonly Vector2 LiftedScale = Vector2.One * 1.08f;

  private bool _lifted;
  private bool _isHeadquarters;
  private bool _faceDown;

  /// <summary>
  /// Ход переворота: 0 — лицом вверх, 1 — рубашкой вверх, 0.5 — карточка повёрнута ребром.
  /// </summary>
  private float _flip;

  /// <summary>
  /// Отображаемая карточка.
  /// </summary>
  public Card Card { get; private set; } = null!;

  /// <summary>
  /// Карточку держат в руке: она чуть крупнее и отбрасывает тень.
  /// </summary>
  public bool Lifted
  {
    get => _lifted;
    set
    {
      _lifted = value;
      Scale = value ? LiftedScale : Vector2.One;
      QueueRedraw();
    }
  }

  /// <summary>
  /// Карточка — штаб теории.
  /// </summary>
  public bool IsHeadquarters
  {
    get => _isHeadquarters;
    set
    {
      _isHeadquarters = value;
      QueueRedraw();
    }
  }

  /// <summary>
  /// Создать отображение карточки.
  /// </summary>
  public static CardView Create(Card card) => new() { Card = card, Name = card.GetType().Name };

  /// <summary>
  /// Перевернуть карточку рубашкой вверх.
  /// </summary>
  public void FlipFaceDown()
  {
    if (_faceDown)
      return;

    _faceDown = true;
    CreateTween()
      .TweenMethod(Callable.From<float>(SetFlip), 0f, 1f, FlipDuration)
      .SetTrans(Tween.TransitionType.Sine)
      .SetEase(Tween.EaseType.InOut);
  }

  public override void _Draw()
  {
    // При перевороте карточка сжимается по горизонтали до ребра и раскрывается другой стороной.
    DrawSetTransform(Vector2.Zero, 0, new Vector2(Mathf.Abs(Mathf.Cos(_flip * Mathf.Pi)), 1));
    var rect = new Rect2(-CardPainter.CardSize / 2, CardPainter.CardSize);
    if (_flip < 0.5f)
      CardPainter.DrawFace(this, rect, Card, _lifted, _isHeadquarters);
    else
      CardPainter.DrawPlainBack(this, rect);
  }

  private void SetFlip(float flip)
  {
    _flip = flip;
    QueueRedraw();
  }
}
