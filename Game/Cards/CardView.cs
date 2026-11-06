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
  private static readonly Color ChipGold = new("e0b84f");
  private static readonly Color WheelColor = new("7fb6ff");
  private const float WheelLineWidth = 3f;
  private static readonly Color ChipBody = new("2b2b2b");
  private static readonly Color ChipDie = new("4fd16a");
  private static readonly float[] ChipPinFractions = [0.25f, 0.5f, 0.75f];
  private const float ChipPinWidth = 2f;

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
  /// Владелец смотрит на закрытую карточку (<see cref="HiddenCard"/>) или на карточку, лежащую рубашкой вверх: видна лицевая сторона.
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
  /// Штаб теории снова открыт: карточка возвращает свой цвет и ленту «CLOSED» снимаем.
  /// </summary>
  public void ClearClosed()
  {
    _closed = false;
    _closedRibbon = false;
    if (_ownerMark is not null)
      _ownerMark.Modulate = Colors.White;
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

  private bool _chipped;
  private bool _wheeled;
  private bool _foiled;

  /// <summary>
  /// На персонажа положена шапочка из фольги (<see cref="Core.Field.PlayingField.IsFoiled"/>): значок в правом верхнем углу.
  /// </summary>
  public bool Foiled
  {
    get => _foiled;
    set
    {
      if (_foiled == value)
        return;

      _foiled = value;
      QueueRedraw();
    }
  }

  /// <summary>
  /// На персонажа положили фишку чипа (<see cref="Core.Field.PlayingField.Chips"/>).
  /// </summary>
  public bool Wheeled
  {
    get => _wheeled;
    set
    {
      _wheeled = value;
      QueueRedraw();
    }
  }

  public bool Chipped
  {
    get => _chipped;
    set
    {
      _chipped = value;
      QueueRedraw();
    }
  }

  public override void _Draw()
  {
    // При перевороте карточка сжимается по горизонтали до ребра и раскрывается другой стороной.
    DrawSetTransform(Vector2.Zero, 0, new Vector2(Mathf.Abs(Mathf.Cos(_flip * Mathf.Pi)), 1));
    var rect = new Rect2(-CardPainter.CardSize / 2, CardPainter.CardSize);
    if (_flip >= 0.5f && !_peeked)
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
    if (_chipped)
      DrawChip(rect);
    if (_wheeled)
      DrawWheel(rect);
    if (_foiled)
      CardPainter.DrawTinFoilHat(this, rect);
  }

  /// <summary>
  /// Значок руля в левом верхнем углу карточки: персонаж вернётся к владельцу на руку после его паса.
  /// </summary>
  private void DrawWheel(Rect2 rect)
  {
    var radius = rect.Size.X * 0.09f;
    var center = new Vector2(rect.Position.X + rect.Size.X * 0.12f, rect.Position.Y + rect.Size.Y * 0.1f);
    DrawArc(center, radius, 0, Mathf.Tau, 24, WheelColor, WheelLineWidth);
    DrawCircle(center, radius * 0.25f, WheelColor);
    // Три спицы: одна вверх и две вниз.
    for (var spoke = 0; spoke < 3; spoke++)
      DrawLine(center, center + Vector2.Up.Rotated(spoke * Mathf.Tau / 3) * radius, WheelColor, WheelLineWidth / 2);
  }

  /// <summary>
  /// Значок компьютерного чипа в правом верхнем углу карточки: на персонажа положили чип («Чипирование»).
  /// Корпус с выводами по четырём сторонам и кристаллом внутри.
  /// </summary>
  private void DrawChip(Rect2 rect)
  {
    var size = rect.Size.X * 0.2f;
    var center = new Vector2(rect.End.X - rect.Size.X * 0.14f, rect.Position.Y + rect.Size.Y * 0.14f);
    var body = new Rect2(center - Vector2.One * size / 2, Vector2.One * size);
    var pinLength = size * 0.22f;

    // Выводы: по три на каждой стороне корпуса.
    foreach (var fraction in ChipPinFractions)
    {
      var x = body.Position.X + body.Size.X * fraction;
      var y = body.Position.Y + body.Size.Y * fraction;
      DrawLine(new Vector2(x, body.Position.Y - pinLength), new Vector2(x, body.Position.Y), ChipGold, ChipPinWidth);
      DrawLine(new Vector2(x, body.End.Y), new Vector2(x, body.End.Y + pinLength), ChipGold, ChipPinWidth);
      DrawLine(new Vector2(body.Position.X - pinLength, y), new Vector2(body.Position.X, y), ChipGold, ChipPinWidth);
      DrawLine(new Vector2(body.End.X, y), new Vector2(body.End.X + pinLength, y), ChipGold, ChipPinWidth);
    }

    DrawRect(body, ChipBody);
    DrawRect(body, ChipGold, filled: false, width: ChipPinWidth);
    // Кристалл в центре корпуса и метка первого вывода в углу.
    DrawRect(body.Grow(-size * 0.25f), ChipDie);
    DrawCircle(body.Position + Vector2.One * size * 0.14f, size * 0.07f, ChipGold);
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
