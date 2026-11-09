using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Game.Cards;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Где на столе лежат ячейки поля. Логика поля (<see cref="PlayingField"/>) всегда сеточная, а стол рисует её
/// либо сеткой, либо радиальным полем (<see cref="Radial"/>): в центре пусто, дальше кольцо общей зоны (теорий),
/// кольцо рынка и кольцо личных штабов. Кольца крутятся мышью.
/// </summary>
/// <remarks>
/// На радиальном поле «вверх» сетки — к центру круга: верх карточек смотрит к центру, стопки теорий растут к центру,
/// поэтому внизу круга поле выглядит так же, как в сетке. Столбцы идут по окружности, строки — по радиусу.
/// </remarks>
public partial class Board
{
  /// <summary>
  /// Радиус пустого места в центре радиального поля.
  /// </summary>
  private static readonly float RadialEmptyRadius = CellSize.Y * 2;

  /// <summary>
  /// Промежуток между кольцами радиального поля.
  /// </summary>
  private static readonly float RingGap = CellSize.Y * 0.5f;

  /// <summary>
  /// На сколько пикселей экрана нужно сдвинуть мышь, чтобы начать крутить кольцо (меньше — это щелчок).
  /// </summary>
  private const float RingDragThreshold = 6f;

  /// <summary>
  /// На какую долю ячейки по строке сдвинута каждая следующая карточка стопки теории: на полкарточки.
  /// </summary>
  private static readonly float StackRowStep = CardPainter.CardSize.Y / 2 / CellSize.Y;

  private const int ZoneArcSegmentsPerCell = 4;

  /// <summary>
  /// Сколько длится поворот кольца личных штабов к ходящему игроку.
  /// </summary>
  private const double RingTurnDuration = 0.4;

  /// <summary>
  /// Кольцо радиального поля.
  /// </summary>
  private enum Ring
  {
    Common,
    Market,
    Players,
  }

  /// <summary>
  /// Как ячейки группы лежат на кольце. Ячейка (столбец, строка) — в точке с радиусом
  /// <c>Radius + (строка − ReferenceRow) · высота ячейки</c> и углом <c>Angle − (столбец − ReferenceColumn) · Step</c>.
  /// </summary>
  private readonly record struct RingPlacement(float Angle, float ReferenceColumn, float Step, float Radius, float ReferenceRow);

  /// <summary>
  /// Радиусы колец и угловые шаги столбцов. Пересчитываются по полю: общая зона растёт, и кольца снаружи раздвигаются.
  /// </summary>
  private readonly record struct RingRadii(
    float CommonInner,
    float TheoryRow,
    float CommonStep,
    float CommonOuter,
    float MarketInner,
    float MarketStep,
    float MarketOuter,
    float PlayersInner,
    float PlayersStep,
    float PlayersOuter);

  /// <summary>
  /// Повороты колец мышью, в радианах.
  /// </summary>
  private readonly Dictionary<Ring, float> _ringTurns = new() { [Ring.Common] = 0, [Ring.Market] = 0, [Ring.Players] = 0 };

  /// <summary>
  /// Столбец общей зоны, который лежит на оси её кольца: середина стартовой общей зоны. Общая зона растёт в стороны,
  /// а уже лежащие теории остаются на своих местах.
  /// </summary>
  private float _commonReferenceColumn;

  private RingDrag? _ringDrag;

  /// <summary>
  /// Радиусы колец, посчитанные для этой общей зоны: остальные зоны не меняются, а общая только растёт.
  /// </summary>
  private (Zone Common, RingRadii Radii)? _radiiCache;

  /// <summary>
  /// Личные штабы по порядку игроков вместе с местом под их карточки теорий (<see cref="PlayingField.TheoryCardAreaOf"/>):
  /// зоны не меняются за партию.
  /// </summary>
  private List<(Zone Zone, Zone TheoryCards)>? _playerSectors;

  private List<(Zone Zone, Zone TheoryCards)> PlayerSectors =>
    _playerSectors ??= Field.PlayerZones.Select(pair => (pair.Value, Field.TheoryCardAreaOf(pair.Key))).ToList();

  /// <summary>
  /// Повернуть кольцо личных штабов так, чтобы штаб игрока оказался внизу круга, у края экрана.
  /// </summary>
  private void TurnPlayersTo(Player player)
  {
    var index = Field.PlayerZones.Keys.ToList().IndexOf(player);
    var count = Field.PlayerZones.Count;
    _ringTurns[Ring.Players] = (index - (count - 1) / 2f) * Mathf.Tau / count;
  }

  /// <summary>
  /// Кольцо, которое крутят мышью: какое, под каким углом была мышь в прошлый раз и сдвинулась ли она достаточно.
  /// </summary>
  private sealed class RingDrag(Ring ring, float angle)
  {
    public Ring Ring { get; } = ring;
    public float LastAngle { get; set; } = angle;
    public float Travel { get; set; }
    public bool Turning { get; set; }
  }

  /// <summary>
  /// Стол в форме кругов: радиальное поле вместо сетки. Задаётся при создании стола.
  /// </summary>
  public bool Radial { get; init; }

  /// <summary>
  /// Центр радиального поля.
  /// </summary>
  private Vector2 RadialCenter => Size / 2;

  /// <summary>
  /// Центр ячейки.
  /// </summary>
  public Vector2 CellCenter(Vector2I cell) => PointAt(cell, cell.X, cell.Y);

  /// <summary>
  /// Поворот ячейки: на радиальном поле верх ячейки смотрит к центру круга; в сетке — 0.
  /// </summary>
  public float CellAngle(Vector2I cell)
  {
    if (!Radial)
      return 0;

    var placement = PlacementOf(cell);
    return AngleOf(placement, cell.X) - Mathf.Pi / 2;
  }

  /// <summary>
  /// Центр карточки в ячейке. Карточки стопки теории лежат ближе друг к другу, чем ячейки: каждая следующая
  /// выше предыдущей на полкарточки, поэтому из-под неё видна только её верхняя половина.
  /// </summary>
  public Vector2 CardCenter(Vector2I cell)
  {
    if (StackOf(Field, cell) is not { } stack)
      return CellCenter(cell);

    var theory = stack.TheoryCell.ToVector2I();
    return PointAt(theory, theory.X, theory.Y - (theory.Y - cell.Y) * StackRowStep);
  }

  /// <summary>
  /// Положение и поворот ячейки на столе: начало координат — центр карточки в ячейке, ось X — вправо по карточке.
  /// </summary>
  public Transform2D SlotTransform(Vector2I cell) => new(CellAngle(cell), CardCenter(cell));

  /// <summary>
  /// Центр зоны.
  /// </summary>
  public Vector2 ZoneCenter(Zone zone) =>
    PointAt(new Vector2I(zone.Left, zone.Top), zone.Left + (zone.Width - 1) / 2f, zone.Top + (zone.Height - 1) / 2f);

  /// <summary>
  /// Точка стола по дробным координатам сетки: (столбец, строка) — центр ячейки, половинки — её края.
  /// </summary>
  /// <param name="anchor">Ячейка, по которой выбирается кольцо (в сетке не важна).</param>
  private Vector2 PointAt(Vector2I anchor, float column, float row)
  {
    if (!Radial)
      return new Vector2((column + 0.5f) * CellSize.X, (row + 0.5f) * CellSize.Y);

    var placement = PlacementOf(anchor);
    var radius = placement.Radius + (row - placement.ReferenceRow) * CellSize.Y;
    return RadialCenter + Vector2.FromAngle(AngleOf(placement, column)) * radius;
  }

  private static float AngleOf(RingPlacement placement, float column) =>
    placement.Angle - (column - placement.ReferenceColumn) * placement.Step;

  /// <summary>
  /// Лежит ли точка стола (в его координатах) в ячейке, с учётом поворота ячейки.
  /// </summary>
  private bool SlotContains(Vector2I cell, Vector2 local)
  {
    var offset = (local - CardCenter(cell)).Rotated(-CellAngle(cell));
    return Mathf.Abs(offset.X) <= CellSize.X / 2 && Mathf.Abs(offset.Y) <= CellSize.Y / 2;
  }

  /// <summary>
  /// Ячейки, которые есть на радиальном поле: общая зона (без ячеек над стопками), рынок, личные штабы и ряды карточек теорий.
  /// </summary>
  private IEnumerable<Vector2I> RadialCells()
  {
    foreach (var cell in Field.CommonZone.Cells)
    {
      var key = cell.ToVector2I();
      if (!IsAboveStack(Field, key))
        yield return key;
    }

    foreach (var cell in Market.Zone.Cells)
      yield return cell.ToVector2I();
    foreach (var (player, zone) in Field.PlayerZones)
    {
      var row = Field.TheoryCardZoneOf(player);
      foreach (var cell in zone.Cells.Concat(row.Cells))
        yield return cell.ToVector2I();
      // Карточки теорий, не поместившиеся в ряд под штабом, лежат дальше: их ячейки тоже есть на поле.
      foreach (var owned in Field.TheoryCards.Where(owned => owned.Owner == player && !row.Contains(owned.Cell)))
        yield return owned.Cell.ToVector2I();
    }
  }

  /// <summary>
  /// Кольцо, к которому относится ячейка: рынок, личный штаб с рядом карточек теорий, остальное — общая зона.
  /// </summary>
  private Ring RingOf(Vector2I cell, out Zone? playerZone, out int playerIndex)
  {
    playerZone = null;
    playerIndex = -1;
    var target = cell.ToCell();
    if (Market.Contains(target))
      return Ring.Market;

    var sectors = PlayerSectors;
    for (var index = 0; index < sectors.Count; index++)
    {
      var (zone, theoryCards) = sectors[index];
      if (zone.Contains(target) || theoryCards.Contains(target))
      {
        playerZone = zone;
        playerIndex = index;
        return Ring.Players;
      }
    }

    return Ring.Common;
  }

  private RingPlacement PlacementOf(Vector2I cell)
  {
    var radii = Radii();
    switch (RingOf(cell, out var playerZone, out var playerIndex))
    {
      case Ring.Market:
      {
        var zone = Market.Zone;
        return new RingPlacement(RingAxis(Ring.Market), zone.Left + (zone.Width - 1) / 2f, radii.MarketStep,
          radii.MarketInner + CellSize.Y / 2, zone.Top);
      }
      case Ring.Players:
      {
        var zone = playerZone!;
        // Игроки сидят по кругу на равных расстояниях; первый слева внизу, как в сетке.
        var count = Field.PlayerZones.Count;
        var angle = RingAxis(Ring.Players) - (playerIndex - (count - 1) / 2f) * Mathf.Tau / count;
        return new RingPlacement(angle, zone.Left + (zone.Width - 1) / 2f, radii.PlayersStep,
          radii.PlayersInner + CellSize.Y / 2, zone.Top);
      }
      default:
        return new RingPlacement(RingAxis(Ring.Common), _commonReferenceColumn, radii.CommonStep,
          radii.TheoryRow, Field.CommonZone.BottomRow);
    }
  }

  /// <summary>
  /// Ось кольца: изначально внизу круга, у игроков, плюс поворот мышью.
  /// </summary>
  private float RingAxis(Ring ring) => Mathf.Pi / 2 + _ringTurns[ring];

  private RingRadii Radii()
  {
    var common = Field.CommonZone;
    if (_radiiCache is { } cache && cache.Common == common)
      return cache.Radii;

    var radii = ComputeRadii(common);
    _radiiCache = (common, radii);
    return radii;
  }

  private RingRadii ComputeRadii(Zone common)
  {
    var cellWidth = CellSize.X;
    var cellHeight = CellSize.Y;

    // Общая зона: стопки растут к центру, поэтому столбцы должны поместиться по окружности её внутреннего края.
    var commonInner = Mathf.Max(RadialEmptyRadius, common.Width * cellWidth / Mathf.Tau);
    var theoryRow = commonInner + (common.Height - 0.5f) * cellHeight;
    // Снаружи ряда теорий — ряд с итогами теорий.
    var commonOuter = theoryRow + 1.5f * cellHeight;

    var market = Market.Zone;
    var marketInner = Mathf.Max(commonOuter + RingGap, market.Width * cellWidth / Mathf.Tau);
    var marketOuter = marketInner + market.Height * cellHeight;

    // Каждому игроку — свой сектор: его штаб и ещё один столбец промежутка до соседа.
    var sectors = PlayerSectors;
    var playerWidth = sectors.Count == 0 ? 0 : sectors.Max(sector => Math.Max(sector.Zone.Width, sector.TheoryCards.Width)) + 1;
    var playerHeight = sectors.Count == 0 ? 0 : sectors.Max(sector => sector.Zone.Height + sector.TheoryCards.Height);
    var zones = sectors;
    var playersInner = Mathf.Max(marketOuter + RingGap, zones.Count * playerWidth * cellWidth / Mathf.Tau);
    var playersOuter = playersInner + playerHeight * cellHeight;

    return new RingRadii(
      commonInner, theoryRow, cellWidth / commonInner, commonOuter,
      marketInner, cellWidth / marketInner, marketOuter,
      playersInner, cellWidth / playersInner, playersOuter);
  }

  /// <summary>
  /// Контур зоны: прямоугольник в сетке, кольцевой сектор на радиальном поле.
  /// </summary>
  private Vector2[] ZoneOutline(Zone zone)
  {
    var anchor = new Vector2I(zone.Left, zone.Top);
    var left = zone.Left - 0.5f;
    var right = zone.Left + zone.Width - 0.5f;
    var top = zone.Top - 0.5f;
    var bottom = zone.BottomRow + 0.5f;
    var segments = Radial ? Mathf.Max(1, zone.Width * ZoneArcSegmentsPerCell) : 1;

    var points = new List<Vector2>();
    for (var i = 0; i <= segments; i++)
      points.Add(PointAt(anchor, Mathf.Lerp(left, right, (float)i / segments), top));
    for (var i = segments; i >= 0; i--)
      points.Add(PointAt(anchor, Mathf.Lerp(left, right, (float)i / segments), bottom));
    return [.. points];
  }

  /// <summary>
  /// Нарисовать прямоугольник ячейки, заданный относительно её центра, с поворотом ячейки.
  /// </summary>
  private void DrawInSlot(Vector2I cell, Action draw)
  {
    DrawSetTransformMatrix(SlotTransform(cell));
    draw();
    DrawSetTransformMatrix(Transform2D.Identity);
  }

  /// <summary>
  /// Прямоугольник ячейки относительно её центра, с отступом от краёв.
  /// </summary>
  private static Rect2 LocalSlot(float inset) => new Rect2(-CellSize / 2, CellSize).Grow(-inset);

  /// <summary>
  /// Плавно повернуть карточку так, как лежит её ячейка, коротким путём. В сетке карточки не поворачиваются.
  /// </summary>
  private void TurnTo(CardView view, Vector2I cell, double duration)
  {
    if (!Radial)
      return;

    var target = view.BaseAngle + Mathf.AngleDifference(view.BaseAngle, CellAngle(cell));
    view.CreateTween()
      .TweenProperty(view, CardView.PropertyName.BaseAngle.ToString(), target, duration)
      .SetTrans(Tween.TransitionType.Cubic)
      .SetEase(Tween.EaseType.Out);
  }

  /// <summary>
  /// Разложить всё заново по текущему радиальному полю: кольцо повернули или общая зона выросла и кольца раздвинулись.
  /// </summary>
  /// <param name="animate">Плавно (поле выросло, сменился ход) или сразу (кольцо крутят мышью).</param>
  /// <param name="duration">Сколько длится плавный переезд.</param>
  private void Relayout(bool animate, double duration = SnapDuration)
  {
    if (!Radial)
      return;

    var time = animate ? duration : 0;
    foreach (var (cell, view) in _views)
      MoveCard(view, CardCenter(cell), CellAngle(cell), time);
    foreach (var (cell, view) in _marketViews)
      MoveCard(view, CellCenter(cell), CellAngle(cell), time);
    foreach (var (owned, view) in _theoryViews)
    {
      var cell = owned.Cell.ToVector2I();
      MoveCard(view, CellCenter(cell), CellAngle(cell), time);
    }

    foreach (var (column, view) in _adepts)
    {
      var cell = column.TheoryCell.ToVector2I();
      MoveNode(view, CellCenter(cell), CellAngle(cell), time);
    }

    foreach (var (cell, view) in _cardAdepts)
      MoveNode(view, CardCenter(cell), CellAngle(cell), time);
    foreach (var (column, meeples) in _leaderMeeples)
    {
      var angle = CellAngle(column.TheoryCell.ToVector2I());
      for (var i = 0; i < meeples.Count; i++)
        MoveNode(meeples[i], LeaderMeeplePosition(column, i, meeples.Count), angle, time);
    }

    _firstPlayerToken.MoveTo(FirstPlayerTokenPosition(Session.Turns.FirstPlayer), animate);
    QueueRedraw();
  }

  /// <param name="duration">0 — переставить сразу.</param>
  private static void MoveCard(CardView view, Vector2 position, float angle, double duration)
  {
    var target = view.BaseAngle + Mathf.AngleDifference(view.BaseAngle, angle);
    if (duration <= 0)
    {
      view.Position = position;
      view.BaseAngle = target;
      return;
    }

    var tween = view.CreateTween().SetParallel();
    tween.TweenProperty(view, Node2D.PropertyName.Position.ToString(), position, duration);
    tween.TweenProperty(view, CardView.PropertyName.BaseAngle.ToString(), target, duration);
  }

  /// <param name="duration">0 — переставить сразу.</param>
  private static void MoveNode(Node2D node, Vector2 position, float angle, double duration)
  {
    var target = node.Rotation + Mathf.AngleDifference(node.Rotation, angle);
    if (duration <= 0)
    {
      node.Position = position;
      node.Rotation = target;
      return;
    }

    var tween = node.CreateTween().SetParallel();
    tween.TweenProperty(node, Node2D.PropertyName.Position.ToString(), position, duration);
    tween.TweenProperty(node, Node2D.PropertyName.Rotation.ToString(), target, duration);
  }

  /// <summary>
  /// Кольцо под точкой стола: промежутки между кольцами делятся пополам, пустое место в центре — общей зоны.
  /// </summary>
  private Ring? RingAt(Vector2 local)
  {
    var radii = Radii();
    var radius = local.DistanceTo(RadialCenter);
    if (radius < (radii.CommonOuter + radii.MarketInner) / 2)
      return Ring.Common;
    if (radius < (radii.MarketOuter + radii.PlayersInner) / 2)
      return Ring.Market;
    return radius <= radii.PlayersOuter + RingGap ? Ring.Players : null;
  }

  /// <summary>
  /// Под мышью то, что берут или выбирают щелчком: карточка или подсвеченная ячейка. Тогда кольцо не крутится.
  /// </summary>
  private bool IsOverInteractive(Vector2 point) =>
    CellAt(point) is { } cell
    && (ViewAt(cell) is not null || MarketViewAt(cell) is not null || TheoryCardAt(cell) is not null || IsAvailable(cell));

  /// <summary>
  /// Кольца радиального поля крутятся левой кнопкой мыши: нажать на пустое место кольца и тянуть влево или вправо.
  /// Кольцо поворачивается вслед за мышью вокруг центра поля.
  /// </summary>
  public override void _UnhandledInput(InputEvent @event)
  {
    if (!Radial)
      return;

    // Точка мыши в координатах стола (стол стоит в начале координат, они же мировые).
    var mouse = @event is InputEventMouse ? ((InputEventMouse)MakeInputLocal(@event)).Position : Vector2.Zero;
    switch (@event)
    {
      case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }:
        _ringDrag = RingAt(mouse) is { } ring && !IsOverInteractive(ToGlobal(mouse))
          ? new RingDrag(ring, (mouse - RadialCenter).Angle())
          : null;
        break;
      case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
        if (_ringDrag is { Turning: true })
          GetViewport().SetInputAsHandled();
        _ringDrag = null;
        break;
      case InputEventMouseMotion motion when _ringDrag is { } drag && (motion.ButtonMask & MouseButtonMask.Left) != 0:
      {
        drag.Travel += motion.Relative.Length();
        if (!drag.Turning && drag.Travel < RingDragThreshold)
          break;

        drag.Turning = true;
        var angle = (mouse - RadialCenter).Angle();
        _ringTurns[drag.Ring] += Mathf.AngleDifference(drag.LastAngle, angle);
        drag.LastAngle = angle;
        Relayout(animate: false);
        GetViewport().SetInputAsHandled();
        break;
      }
    }
  }
}
