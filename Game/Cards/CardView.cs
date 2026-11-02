using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Game.Ui;
using Godot;

namespace ConspiracyLeague.Game.Cards;

/// <summary>
/// Карточка на столе. Начало координат узла — центр карточки.
/// </summary>
public partial class CardView : Node2D
{
  private const double FlipDuration = 0.3;
  private const double RotateDuration = 0.3;
  private const float OwnerMarkDropHeight = 40f;
  private const double OwnerMarkDropDuration = 0.4;

  private static readonly Vector2 LiftedScale = Vector2.One * 1.08f;
  private static readonly Color ClosedTint = new(0.6f, 0.6f, 0.6f);

  private bool _lifted;
  private bool _isHeadquarters;
  private bool _faceDown;
  private bool _rotated;
  private bool _shielded;
  private bool _closed;
  private bool _closedRibbon;
  private bool _peeked;
  private int _successModifier;
  private Player? _owner;
  private MeepleView? _ownerMark;

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
  /// Владелец карточки в теории: справа от названия теории стоит мипл его цвета.
  /// <c>null</c> — карточка ничья (например, лежит в личном штабе).
  /// </summary>
  public Player? CardOwner
  {
    get => _owner;
    set
    {
      _owner = value;
      _ownerMark?.QueueFree();
      _ownerMark = value is null ? null : CreateOwnerMark(value);
      QueueRedraw();
    }
  }

  /// <summary>
  /// На сколько процентов изменена вероятность успеха персонажа: на карточке зелёная стрелка вверх и, например, «+10%»
  /// или красная стрелка вниз и «-10%». 0 — не изменена.
  /// </summary>
  public int SuccessModifier
  {
    get => _successModifier;
    set
    {
      _successModifier = value;
      QueueRedraw();
    }
  }

  /// <summary>
  /// Владелец подглядывает в закрытую карточку (<see cref="HiddenCard"/>): видна её настоящая лицевая сторона.
  /// Остальные карточки это не меняет.
  /// </summary>
  public bool Peeked
  {
    get => _peeked;
    set
    {
      _peeked = value;
      QueueRedraw();
    }
  }

  /// <summary>
  /// Создать отображение карточки.
  /// </summary>
  public static CardView Create(Card card) => new() { Card = card, Name = card.GetType().Name };

  /// <summary>
  /// Показать на месте этой карточки другую (бездомного обучили, лидера закрыли).
  /// </summary>
  public void ShowCard(Card card)
  {
    Card = card;
    QueueRedraw();
  }

  /// <summary>
  /// Штаб теории, к которому относится карточка, закрыт: карточка сереет.
  /// </summary>
  /// <param name="withRibbon">Повесить ленту «CLOSED» — на первую карточку штаба.</param>
  public void MarkClosed(bool withRibbon)
  {
    _closed = true;
    _closedRibbon = withRibbon;
    if (_ownerMark is not null)
      _ownerMark.Modulate = ClosedTint;
    QueueRedraw();
  }

  /// <summary>
  /// Карточку защищает полицейский: в углу нарисован щит.
  /// </summary>
  public bool Shielded
  {
    get => _shielded;
    set
    {
      if (_shielded == value)
        return;

      _shielded = value;
      QueueRedraw();
    }
  }

  /// <summary>
  /// Карточка повёрнута на 90 градусов: она сыграла быстрое действие и может сыграть ещё одно.
  /// </summary>
  public bool Rotated
  {
    get => _rotated;
    set
    {
      if (_rotated == value)
        return;

      _rotated = value;
      CreateTween()
        .TweenProperty(this, Node2D.PropertyName.Rotation.ToString(), value ? Mathf.Pi / 2 : 0f, RotateDuration)
        .SetTrans(Tween.TransitionType.Cubic)
        .SetEase(Tween.EaseType.Out);
    }
  }

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

  /// <summary>
  /// Перевернуть карточку лицевой стороной вверх.
  /// </summary>
  public void FlipFaceUp()
  {
    if (!_faceDown)
      return;

    _faceDown = false;
    CreateTween()
      .TweenMethod(Callable.From<float>(SetFlip), 1f, 0f, FlipDuration)
      .SetTrans(Tween.TransitionType.Sine)
      .SetEase(Tween.EaseType.InOut);
  }

  public override void _Draw()
  {
    // При перевороте карточка сжимается по горизонтали до ребра и раскрывается другой стороной.
    DrawSetTransform(Vector2.Zero, 0, new Vector2(Mathf.Abs(Mathf.Cos(_flip * Mathf.Pi)), 1));
    var rect = new Rect2(-CardPainter.CardSize / 2, CardPainter.CardSize);
    if (_flip >= 0.5f)
    {
      CardPainter.DrawPlainBack(this, rect);
      DrawShieldIfNeeded(rect);
      return;
    }

    if (Card is HiddenCard { IsRevealed: false } hidden && !_peeked)
      CardPainter.DrawHiddenBack(this, rect, hidden);
    else
    {
      var face = Card is HiddenCard shown ? shown.Face : Card;
      CardPainter.DrawFace(this, rect, face, _lifted, _isHeadquarters, withOwnerMark: _owner is not null);
      if (_successModifier != 0)
        CardPainter.DrawSuccessModifier(this, rect, _successModifier);
      if (Card is HiddenCard { IsRevealed: false })
        CardPainter.DrawPeekOverlay(this, rect);
    }

    if (_closed)
      CardPainter.DrawClosedOverlay(this, rect);
    if (_closedRibbon)
      CardPainter.DrawClosedRibbon(this, rect);
    DrawShieldIfNeeded(rect);
  }

  private void DrawShieldIfNeeded(Rect2 rect)
  {
    if (_shielded)
      CardPainter.DrawShield(this, rect);
  }

  /// <summary>
  /// Поставить мипл владельца: он падает на карточку сверху.
  /// </summary>
  private MeepleView CreateOwnerMark(Player owner)
  {
    var meeple = new MeepleView
    {
      Name = "OwnerMark",
      Color = PlayerPalette.MainOf(owner.Color),
      Position = CardPainter.OwnerMarkPosition(new Rect2(-CardPainter.CardSize / 2, CardPainter.CardSize)),
      Scale = Vector2.One * CardPainter.OwnerMarkScale,
      Height = OwnerMarkDropHeight,
    };
    AddChild(meeple);
    meeple.CreateTween()
      .TweenProperty(meeple, MeepleView.PropertyName.Height.ToString(), 0f, OwnerMarkDropDuration)
      .SetTrans(Tween.TransitionType.Bounce)
      .SetEase(Tween.EaseType.Out);
    return meeple;
  }

  private void SetFlip(float flip)
  {
    _flip = flip;
    QueueRedraw();
  }
}
