using Godot;

namespace ConspiracyLeague.Game.Cards;

/// <summary>
/// Фигурки адептов на карточке теории. Начало координат узла — центр карточки.
/// </summary>
/// <remarks>
/// Фигурки стоят рядами снизу вверх; когда их много, ряды сдвигаются плотнее. Сколько всего адептов,
/// подписано под теорией или значком <see cref="Total"/>, поэтому фигурок рисуется не больше <see cref="MaxShown"/>.
/// </remarks>
public partial class AdeptsView : Node2D
{
  private const int PerRow = 5;
  private const int MaxShown = 20;
  private const float DropHeight = 70f;
  private const double DropDuration = 0.45;
  private const double DropStagger = 0.12;
  private const double ShiftDuration = 0.2;

  private static readonly Color BadgeColor = new("7a4fa3");
  private static readonly Color BadgeTextColor = Colors.White;

  /// <summary>
  /// Область карточки, где стоят ступни фигурок, относительно центра карточки: первый ряд — у нижнего края,
  /// верхний ряд не заходит головами на заголовок.
  /// </summary>
  private static readonly Rect2 Area = new(
    -CardPainter.CardSize.X / 2 + 4,
    -CardPainter.CardSize.Y * 0.32f + MeepleView.Size.Y,
    CardPainter.CardSize.X - 8,
    CardPainter.CardSize.Y * 0.795f - MeepleView.Size.Y);

  private readonly List<MeepleView> _meeples = [];
  private int? _total;

  /// <summary>
  /// Сколько всего адептов — рисуется значком в углу карточки. <c>null</c> — значка нет
  /// (у теории количество подписано под стопкой).
  /// </summary>
  public int? Total
  {
    get => _total;
    set
    {
      _total = value;
      QueueRedraw();
    }
  }

  public override void _Ready()
  {
    // Фигурки задних рядов (выше на экране) рисуются раньше передних.
    YSortEnabled = true;
  }

  public override void _Draw()
  {
    if (_total is not { } total)
      return;

    // Значок в правом верхнем углу карточки, над заголовком.
    var radius = CardPainter.CardSize.X * 0.13f;
    var center = new Vector2(CardPainter.CardSize.X / 2 - radius * 0.6f, -CardPainter.CardSize.Y / 2 + radius * 0.6f);
    DrawCircle(center, radius, BadgeColor);
    DrawCircle(center, radius, BadgeTextColor, filled: false, width: 2);
    var area = new Rect2(center - Vector2.One * radius, Vector2.One * radius * 2);
    CardPainter.DrawCenteredText(this, total.ToString(), area, (int)(radius * 1.1f), BadgeTextColor);
  }

  /// <summary>
  /// Поставить на карточку новые фигурки: они по очереди падают сверху.
  /// </summary>
  /// <param name="count">Сколько адептов пришло.</param>
  public void Add(int count)
  {
    var added = new List<MeepleView>();
    for (var i = Mathf.Min(count, MaxShown - _meeples.Count); i > 0; i--)
    {
      var meeple = new MeepleView { Height = DropHeight, Modulate = Colors.Transparent };
      _meeples.Add(meeple);
      added.Add(meeple);
      AddChild(meeple);
    }

    for (var i = 0; i < _meeples.Count; i++)
    {
      var meeple = _meeples[i];
      if (!added.Contains(meeple))
      {
        meeple.CreateTween()
          .TweenProperty(meeple, Node2D.PropertyName.Position.ToString(), PositionOf(i), ShiftDuration)
          .SetTrans(Tween.TransitionType.Cubic)
          .SetEase(Tween.EaseType.Out);
        continue;
      }

      meeple.Position = PositionOf(i);
      var tween = meeple.CreateTween();
      tween.TweenInterval(DropStagger * added.IndexOf(meeple));
      tween.TweenProperty(meeple, CanvasItem.PropertyName.Modulate.ToString(), Colors.White, DropDuration / 3);
      tween.Parallel()
        .TweenProperty(meeple, MeepleView.PropertyName.Height.ToString(), 0f, DropDuration)
        .SetTrans(Tween.TransitionType.Bounce)
        .SetEase(Tween.EaseType.Out);
    }
  }

  /// <summary>
  /// Адептов стало меньше: лишние фигурки тают, остальные встают плотнее.
  /// </summary>
  /// <param name="total">Сколько адептов осталось.</param>
  public void ShrinkTo(int total)
  {
    while (_meeples.Count > Mathf.Max(0, Mathf.Min(total, MaxShown)))
    {
      var meeple = _meeples[^1];
      _meeples.RemoveAt(_meeples.Count - 1);
      var fade = meeple.CreateTween();
      fade.TweenProperty(meeple, CanvasItem.PropertyName.Modulate.ToString(), Colors.Transparent, ShiftDuration);
      fade.TweenCallback(Callable.From(meeple.QueueFree));
    }

    for (var i = 0; i < _meeples.Count; i++)
    {
      _meeples[i].CreateTween()
        .TweenProperty(_meeples[i], Node2D.PropertyName.Position.ToString(), PositionOf(i), ShiftDuration)
        .SetTrans(Tween.TransitionType.Cubic)
        .SetEase(Tween.EaseType.Out);
    }
  }

  /// <summary>
  /// Где стоит фигурка: ряды по <see cref="PerRow"/> штук, нулевой ряд — передний, внизу карточки.
  /// </summary>
  private Vector2 PositionOf(int index)
  {
    var rows = (_meeples.Count + PerRow - 1) / PerRow;
    var row = index / PerRow;
    var inRow = Mathf.Min(PerRow, _meeples.Count - row * PerRow);

    var columnStep = Area.Size.X / PerRow;
    var x = Area.GetCenter().X + (index % PerRow - (inRow - 1) / 2f) * columnStep;

    var rowStep = rows > 1 ? Mathf.Min(MeepleView.Size.Y * 0.8f, Area.Size.Y / (rows - 1)) : 0;
    return new Vector2(x, Area.End.Y - row * rowStep);
  }
}
