using Godot;

namespace ConspiracyLeague.Game.Cards;

/// <summary>
/// Фигурки адептов. Начало координат узла — центр карточки, к которой они относятся.
/// </summary>
/// <remarks>
/// <para>
/// Адепты штаба теории (задан <see cref="GroundsSource"/>) бродят вокруг карточек штаба: стоят, потом идут
/// в случайную точку неподалёку. На карточки они заходят неглубоко — не дальше 2–3 шагов от края, а изредка
/// кто-нибудь проходит через штаб насквозь.
/// </para>
/// <para>
/// Без <see cref="GroundsSource"/> (адепты на карточке личного штаба) фигурки стоят на карточке рядами снизу вверх;
/// когда их много, ряды сдвигаются плотнее.
/// </para>
/// <para>
/// Сколько всего адептов, подписано под теорией или значком <see cref="Total"/>, поэтому фигурок рисуется
/// не больше <see cref="MaxShown"/>.
/// </para>
/// </remarks>
public partial class AdeptsView : Node2D
{
  /// <summary>
  /// Где бродят адепты, в координатах узла: <see cref="Home"/> — карточки штаба вместе, <see cref="Cards"/> —
  /// все карточки поблизости (свои и чужие), на которые заходить можно лишь неглубоко.
  /// </summary>
  public readonly record struct Grounds(Rect2 Home, IReadOnlyList<Rect2> Cards);

  /// <summary>
  /// Что делает бродящий адепт: до какого времени стоит, куда идёт и как глубоко готов заходить на карточки.
  /// </summary>
  private sealed class Wanderer
  {
    public double RestUntil { get; set; }
    public Vector2 Target { get; set; }
    public float Reach { get; init; }

    /// <summary>
    /// Свой прогулочный темп: шагов в секунду.
    /// </summary>
    public float Pace { get; init; }
  }

  private const int PerRow = 5;
  private const int MaxShown = 20;
  private const float DropHeight = 70f;
  private const double DropDuration = 0.45;
  private const double DropStagger = 0.12;
  private const double ShiftDuration = 0.2;
  private const double MinWalkDuration = 0.25;
  private const double MaxWalkDuration = 0.9;

  /// <summary>
  /// Насколько далеко от карточек штаба отходят адепты.
  /// </summary>
  private const float Margin = 34f;

  /// <summary>
  /// Ближе этого адепты не встают друг к другу.
  /// </summary>
  private const float Personal = 11f;

  private const float LeaveDistance = 60f;

  /// <summary>
  /// Адепты ближе этого к месту клика разбегаются.
  /// </summary>
  private const float ScareRadius = 60f;

  /// <summary>
  /// Клики по штабу, между которыми меньше этого времени, считаются подряд: адепты бегут всё быстрее.
  /// </summary>
  private const double ScareStreak = 1.5;

  /// <summary>
  /// Во сколько раз быстрее своего темпа бегут перепуганные адепты, не больше.
  /// </summary>
  private const float MaxScareSpeed = 3f;

  private static readonly float[] StandingHeadings = [90, 135, 180, 225, 270];

  /// <summary>
  /// Во сколько раз фигурки крупнее своего рисунка (<see cref="WalkingAdeptView"/>).
  /// </summary>
  private const float FigureScale = 1.25f;

  private static readonly Color[] SkinTones = [new("f1d0ae"), new("e8c9a0"), new("d9a57a"), new("b9805a"), new("8d5a3b")];
  private static readonly Color[] HairColors = [new("2b211c"), new("4a3426"), new("6b4a2e"), new("a0773f"), new("d9c08a"), new("8c8c8c")];

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

  private readonly List<WalkingAdeptView> _meeples = [];
  private readonly Dictionary<WalkingAdeptView, Tween> _walks = [];
  private readonly Dictionary<WalkingAdeptView, Wanderer> _wanderers = [];
  private readonly RandomNumberGenerator _random = new();
  private int? _total;
  private double _clock;
  private double _nextCrossing;
  private double _crossingUntil;
  private int _scareStreak;
  private double _lastScare = double.NegativeInfinity;

  /// <summary>
  /// Облик адептов: у некоторых теорий свой (см. <see cref="MeepleView.MeepleLook"/>).
  /// </summary>
  public MeepleView.MeepleLook Look { get; init; } = MeepleView.MeepleLook.Wooden;

  /// <summary>
  /// Цвет адептов (тела, мантии или костюма); по умолчанию серый.
  /// </summary>
  public Color Color { get; init; } = MeepleView.Gray;

  /// <summary>
  /// Где бродить: стопка штаба растёт, соседние карточки появляются, поэтому место спрашивается заново
  /// перед каждым выбором пути. <c>null</c> — адепты стоят рядами на карточке.
  /// </summary>
  public Func<Grounds>? GroundsSource { get; init; }

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

  private bool Wandering => GroundsSource is not null;

  public override void _Ready()
  {
    // Задние (выше на экране) фигурки рисуются раньше передних.
    YSortEnabled = true;
    _random.Randomize();
    _nextCrossing = _random.RandfRange(6, 15);
  }

  public override void _Process(double delta)
  {
    if (GroundsSource is not { } source)
      return;

    _clock += delta;
    Grounds? grounds = null;
    foreach (var meeple in _meeples)
    {
      var wanderer = _wanderers[meeple];
      if (_walks.ContainsKey(meeple) || meeple.Height > 0 || _clock < wanderer.RestUntil)
        continue;

      grounds ??= source();
      var cross = _clock >= _nextCrossing && _clock >= _crossingUntil && _random.Randf() < 0.25f;
      var target = cross ? CrossingTarget(meeple.Position, grounds.Value) : null;
      cross = target is not null;
      target ??= WanderTarget(meeple, wanderer, grounds.Value);
      if (target is not { } point)
      {
        wanderer.RestUntil = _clock + 0.5;
        continue;
      }

      wanderer.Target = point;
      meeple.StepsPerSecond = wanderer.Pace;
      var duration = WalkTo(meeple, point, hurry: false);
      wanderer.RestUntil = _clock + duration + _random.RandfRange(0.6f, 4f);
      if (cross)
      {
        _crossingUntil = _clock + duration;
        _nextCrossing = _crossingUntil + _random.RandfRange(10, 25);
      }
    }
  }

  public override void _Input(InputEvent @event)
  {
    if (GroundsSource is null || @event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
      return;
    // Клик по окну интерфейса поверх стола штаб не задевает.
    if (GetViewport().GuiGetHoveredControl() is not null)
      return;

    // Клик не забирается: карточку под ним по-прежнему можно выбрать.
    Scare(ToLocal(GetGlobalMousePosition()));
  }

  /// <summary>
  /// Клик по штабу теории в точке <paramref name="point"/> (в координатах узла): адепты рядом с ней убегают от неё.
  /// Чем больше кликов подряд, тем быстрее бегут (см. <see cref="ScareSpeed"/>).
  /// </summary>
  public void Scare(Vector2 point)
  {
    if (GroundsSource is not { } source || !source().Home.Grow(Margin + 10).HasPoint(point))
      return;

    _scareStreak = _clock - _lastScare < ScareStreak ? _scareStreak + 1 : 1;
    _lastScare = _clock;
    var speed = ScareSpeed(_scareStreak);

    foreach (var meeple in _meeples)
    {
      if (meeple.Height > 0 || meeple.Position.DistanceTo(point) > ScareRadius || !_wanderers.TryGetValue(meeple, out var wanderer))
        continue;

      var away = meeple.Position - point;
      var direction = away.LengthSquared() < 1 ? Vector2.FromAngle(_random.Randf() * Mathf.Tau) : away.Normalized();
      direction = direction.Rotated(_random.RandfRange(-0.35f, 0.35f));
      var target = meeple.Position + direction * _random.RandfRange(35, 60);

      meeple.StepsPerSecond = wanderer.Pace * speed;
      meeple.Haste = (speed - 1) / (MaxScareSpeed - 1);
      var duration = WalkTo(meeple, target, hurry: false, arrived: () => meeple.Haste = 0);
      wanderer.Target = target;
      wanderer.RestUntil = _clock + duration + _random.RandfRange(0.4f, 1.5f);
    }
  }

  /// <summary>
  /// Во сколько раз быстрее своего шага бегут адепты на <paramref name="streak"/>-й клик подряд:
  /// 1–2-й — своим шагом, 3–5-й — в 1,5 раза, 6–8-й — в 2, 9–11-й — в 2,5, с 12-го — в <see cref="MaxScareSpeed"/>.
  /// </summary>
  private static float ScareSpeed(int streak) => streak switch
  {
    <= 2 => 1f,
    <= 5 => 1.5f,
    <= 8 => 2f,
    <= 11 => 2.5f,
    _ => MaxScareSpeed,
  };

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
  /// Добавить фигурки: они по очереди падают сверху — на карточку или, у штаба теории, на свободное место рядом.
  /// </summary>
  /// <param name="count">Сколько адептов пришло.</param>
  public void Add(int count)
  {
    var added = new List<WalkingAdeptView>();
    for (var i = Mathf.Min(count, MaxShown - _meeples.Count); i > 0; i--)
    {
      var meeple = new WalkingAdeptView
      {
        Height = DropHeight, Modulate = Colors.Transparent, Look = Look, Color = Color, Build = WalkingAdeptView.BodyBuild.Slim,
        Phase = _random.Randf() * Mathf.Tau, Heading = StandingHeadings[_random.RandiRange(0, StandingHeadings.Length - 1)],
        SkinTone = SkinTones[_random.RandiRange(0, SkinTones.Length - 1)], HairColor = HairColors[_random.RandiRange(0, HairColors.Length - 1)],
        Scale = Vector2.One * FigureScale,
      };
      _meeples.Add(meeple);
      added.Add(meeple);
      AddChild(meeple);
    }

    var grounds = GroundsSource?.Invoke();
    for (var i = 0; i < _meeples.Count; i++)
    {
      var meeple = _meeples[i];
      if (!added.Contains(meeple))
      {
        if (!Wandering)
          WalkTo(meeple, PositionOf(i));
        continue;
      }

      var stagger = DropStagger * added.IndexOf(meeple);
      if (grounds is { } place)
      {
        var wanderer = new Wanderer { Reach = StepsInto(meeple), Pace = _random.RandfRange(2.2f, 3f) };
        _wanderers[meeple] = wanderer;
        meeple.Position = SpawnPoint(place, wanderer.Reach);
        wanderer.Target = meeple.Position;
        wanderer.RestUntil = _clock + stagger + DropDuration + _random.RandfRange(0.3f, 2.5f);
      }
      else
        meeple.Position = PositionOf(i);

      var tween = meeple.CreateTween();
      tween.TweenInterval(stagger);
      tween.TweenProperty(meeple, CanvasItem.PropertyName.Modulate.ToString(), Colors.White, DropDuration / 3);
      tween.Parallel()
        .TweenProperty(meeple, WalkingAdeptView.PropertyName.Height.ToString(), 0f, DropDuration)
        .SetTrans(Tween.TransitionType.Bounce)
        .SetEase(Tween.EaseType.Out);
    }
  }

  /// <summary>
  /// Адептов стало меньше: лишние фигурки уходят прочь (с карточки — к ближнему краю, от штаба — в сторону от него)
  /// и тают; на карточке остальные подходят плотнее.
  /// </summary>
  /// <param name="total">Сколько адептов осталось.</param>
  public void ShrinkTo(int total)
  {
    var home = GroundsSource?.Invoke().Home;
    while (_meeples.Count > Mathf.Max(0, Mathf.Min(total, MaxShown)))
    {
      var meeple = _meeples[^1];
      _meeples.RemoveAt(_meeples.Count - 1);
      _wanderers.Remove(meeple);
      var away = home is { } rect
        ? meeple.Position + (meeple.Position - rect.GetCenter()).Normalized() * LeaveDistance
        : meeple.Position with { X = (meeple.Position.X < 0 ? -1 : 1) * CardPainter.CardSize.X / 2 };
      var duration = WalkTo(meeple, away, hurry: home is null);
      _walks.Remove(meeple);
      var fade = meeple.CreateTween();
      fade.TweenProperty(meeple, CanvasItem.PropertyName.Modulate.ToString(), Colors.Transparent, Mathf.Max(duration, ShiftDuration))
        .SetEase(Tween.EaseType.In);
      fade.TweenCallback(Callable.From(meeple.QueueFree));
    }

    if (Wandering)
      return;

    for (var i = 0; i < _meeples.Count; i++)
      WalkTo(_meeples[i], PositionOf(i));
  }

  /// <summary>
  /// Фигурка идёт пешком к точке, повернувшись в ближайшее из восьми направлений.
  /// </summary>
  /// <param name="hurry">Уложиться в короткое время (перестановка рядов): тогда шаг чаще, чтобы ступни не скользили.
  /// Иначе — прогулочным шагом с текущей частотой.</param>
  /// <returns>Сколько секунд идти.</returns>
  /// <param name="arrived">Что сделать, когда фигурка дойдёт.</param>
  private double WalkTo(WalkingAdeptView meeple, Vector2 target, bool hurry = true, Action? arrived = null)
  {
    if (_walks.Remove(meeple, out var previous))
      previous.Kill();

    var distance = meeple.Position.DistanceTo(target);
    if (distance < 0.5f)
    {
      meeple.Position = target;
      meeple.Walking = false;
      arrived?.Invoke();
      return 0;
    }

    meeple.FaceTowards(target - meeple.Position);
    // Шаг и скорость фигурки — в её собственных пикселях, а идёт она в пикселях узла.
    var step = meeple.ScreenStepLength * meeple.Scale.X;
    double duration;
    if (hurry)
    {
      duration = Mathf.Clamp(distance / (step * 3), MinWalkDuration, MaxWalkDuration);
      meeple.StepsPerSecond = distance / (float)duration / step;
    }
    else
      duration = distance / (meeple.WalkSpeed * meeple.Scale.X);

    meeple.Walking = true;
    var tween = meeple.CreateTween();
    tween.TweenProperty(meeple, Node2D.PropertyName.Position.ToString(), target, duration);
    tween.TweenCallback(Callable.From(() =>
    {
      meeple.Walking = false;
      _walks.Remove(meeple);
      arrived?.Invoke();
    }));
    _walks[meeple] = tween;
    return duration;
  }

  /// <summary>
  /// Насколько глубоко этот адепт готов заходить на карточки: 2–3 его шага.
  /// </summary>
  private float StepsInto(WalkingAdeptView meeple) => meeple.StepLength * meeple.Scale.X * _random.RandfRange(2f, 3f);

  /// <summary>
  /// Куда пойти погулять: недалеко от текущего места, в полосе вокруг штаба, не заходя на карточки глубже
  /// <see cref="Wanderer.Reach"/> ни в одной точке пути и не наступая на других.
  /// </summary>
  private Vector2? WanderTarget(WalkingAdeptView meeple, Wanderer wanderer, Grounds grounds)
  {
    var area = grounds.Home.Grow(Margin);
    var from = meeple.Position;
    var lost = !area.HasPoint(from);
    for (var attempt = 0; attempt < 24; attempt++)
    {
      // Заблудившийся (штаб сдвинулся или вырос) возвращается в любую точку полосы.
      var candidate = lost || attempt >= 16
        ? RandomPoint(area)
        : from + Vector2.FromAngle(_random.Randf() * Mathf.Tau) * _random.RandfRange(14, 55);
      if (!area.HasPoint(candidate) || Crowded(candidate, meeple))
        continue;
      if (MaxDepth(grounds.Cards, from, candidate) > (lost ? float.MaxValue : wanderer.Reach) || Depth(grounds.Cards, candidate) > wanderer.Reach)
        continue;
      return candidate;
    }

    return null;
  }

  /// <summary>
  /// Куда пройти через штаб насквозь: на другую его сторону — справа налево, слева направо, сверху вниз или снизу вверх.
  /// </summary>
  private Vector2? CrossingTarget(Vector2 from, Grounds grounds)
  {
    var home = grounds.Home;
    var center = home.GetCenter();
    var across = (from - center) / (home.Size / 2);
    if (Mathf.Max(Mathf.Abs(across.X), Mathf.Abs(across.Y)) < 0.9f)
      return null;

    var target = Mathf.Abs(across.X) > Mathf.Abs(across.Y)
      ? new Vector2(2 * center.X - from.X, from.Y + _random.RandfRange(-25, 25))
      : new Vector2(from.X + _random.RandfRange(-25, 25), 2 * center.Y - from.Y);
    return home.Grow(Margin).HasPoint(target) && Depth(grounds.Cards, target) < 2 ? target : null;
  }

  /// <summary>
  /// Где встать новому адепту: в полосе вокруг штаба, на краю карточки не глубже половины его досягаемости.
  /// </summary>
  private Vector2 SpawnPoint(Grounds grounds, float reach)
  {
    var area = grounds.Home.Grow(Margin);
    var best = RandomPoint(area);
    for (var attempt = 0; attempt < 30; attempt++)
    {
      var candidate = RandomPoint(area);
      if (Depth(grounds.Cards, candidate) > reach * 0.5f)
        continue;
      best = candidate;
      if (!Crowded(candidate, null))
        break;
    }

    return best;
  }

  private Vector2 RandomPoint(Rect2 area) =>
    area.Position + new Vector2(_random.Randf() * area.Size.X, _random.Randf() * area.Size.Y);

  /// <summary>
  /// Рядом с точкой уже стоит или собирается встать другой адепт.
  /// </summary>
  private bool Crowded(Vector2 point, WalkingAdeptView? self) =>
    _meeples.Any(other => other != self && (other.Position.DistanceTo(point) < Personal
      || (_wanderers.TryGetValue(other, out var wanderer) && wanderer.Target.DistanceTo(point) < Personal)));

  /// <summary>
  /// Как глубоко точка на карточках: расстояние до ближнего края самой «глубокой» из них; 0 — вне карточек.
  /// </summary>
  private static float Depth(IReadOnlyList<Rect2> cards, Vector2 point)
  {
    var deepest = 0f;
    foreach (var card in cards)
    {
      if (!card.HasPoint(point))
        continue;
      var depth = Mathf.Min(Mathf.Min(point.X - card.Position.X, card.End.X - point.X), Mathf.Min(point.Y - card.Position.Y, card.End.Y - point.Y));
      deepest = Mathf.Max(deepest, depth);
    }

    return deepest;
  }

  /// <summary>
  /// Самая глубокая точка пути по карточкам.
  /// </summary>
  private static float MaxDepth(IReadOnlyList<Rect2> cards, Vector2 from, Vector2 to)
  {
    var steps = Mathf.Max(1, Mathf.CeilToInt(from.DistanceTo(to) / 3));
    var deepest = 0f;
    for (var i = 0; i <= steps; i++)
      deepest = Mathf.Max(deepest, Depth(cards, from.Lerp(to, (float)i / steps)));
    return deepest;
  }

  /// <summary>
  /// Где стоит фигурка на карточке: ряды по <see cref="PerRow"/> штук, нулевой ряд — передний, внизу карточки.
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
