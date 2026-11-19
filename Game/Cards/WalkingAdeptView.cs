using Godot;

namespace ConspiracyLeague.Game.Cards;

/// <summary>
/// Шагающий человечек-адепт в 2.5D. Может идти в любую сторону: в профиль, анфас, спиной и по диагоналям.
/// Начало координат узла — точка, где человечек стоит на столе (под бёдрами).
/// </summary>
/// <remarks>
/// Скелет объёмный: бёдра и плечи разнесены в стороны, суставы считаются в осях тела (вперёд, вверх, вправо)
/// и проецируются на экран: стол виден сверху под углом, дальнее — выше и чуть правее. Части тела рисуются
/// от дальних к ближним. Походка считается по фазе шага: опорная нога прямая, маховая сгибается в колене,
/// руки идут навстречу ногам, а бёдра поднимаются так, чтобы опорная ступня всегда стояла на столе.
/// </remarks>
public partial class WalkingAdeptView : Node2D
{
  /// <summary>
  /// Телосложение человечка.
  /// </summary>
  public enum BodyBuild
  {
    /// <summary>Коренастый, как деревянный мипл: толстые руки и ноги, крупная голова, видна толщина.</summary>
    Chunky,

    /// <summary>Стройный: длиннее ноги, тоньше руки и ноги, голова меньше, объём — светом и тенью.</summary>
    Slim,
  }

  /// <summary>
  /// Размеры частей тела в пикселях. <c>Chest</c>/<c>Waist</c> — полутолщина туловища (видна в профиль),
  /// <c>ChestWidth</c>/<c>WaistWidth</c> — полуширина (видна анфас).
  /// </summary>
  private sealed record Proportions(
    float Thigh,
    float Shin,
    float Leg,
    float Foot,
    float Torso,
    float Chest,
    float Waist,
    float ChestWidth,
    float WaistWidth,
    float HipHalf,
    float ShoulderHalf,
    float Head,
    float UpperArm,
    float Forearm,
    float Arm,
    float Thick);

  private static readonly Proportions ChunkyBody = new(
    Thigh: 5f, Shin: 4.6f, Leg: 2.3f, Foot: 2.4f, Torso: 7f, Chest: 3.9f, Waist: 3.3f, ChestWidth: 5f, WaistWidth: 4.3f,
    HipHalf: 2.1f, ShoulderHalf: 4.3f, Head: 4.4f, UpperArm: 4.2f, Forearm: 3.8f, Arm: 1.9f, Thick: 3f);

  private static readonly Proportions SlimBody = new(
    Thigh: 5.8f, Shin: 5.6f, Leg: 1.45f, Foot: 2.6f, Torso: 8f, Chest: 2.9f, Waist: 2.3f, ChestWidth: 3.5f, WaistWidth: 2.8f,
    HipHalf: 1.4f, ShoulderHalf: 3.3f, Head: 3.3f, UpperArm: 4.6f, Forearm: 4.3f, Arm: 1.15f, Thick: 0f);

  private const float LegSwing = 0.48f;
  private const float KneeBend = 0.95f;
  private const float ArmSwing = 0.55f;
  private const float ElbowBend = 0.35f;
  private const float Lean = 0.07f;
  private const float OutlineWidth = 0.55f;
  private const int ArcSegments = 7;

  /// <summary>
  /// Проекция глубины: на сколько пикселей поднимается (и сдвигается вправо) точка, отодвинутая от зрителя на пиксель.
  /// </summary>
  private const float DepthScale = 0.55f;

  private const float DepthShear = 0.25f;

  /// <summary>
  /// Направление толщины у коренастого: как у <see cref="MeepleView"/>, человечка видно чуть сверху и справа.
  /// </summary>
  private static readonly Vector2 DepthDirection = new Vector2(0.6f, -0.8f).Normalized();

  /// <summary>
  /// Откуда падает свет: сверху слева, с противоположной от толщины стороны.
  /// </summary>
  private static readonly Vector2 LightDirection = new Vector2(-0.55f, -0.85f).Normalized();

  private static readonly Color ShadowColor = new(0, 0, 0, 0.35f);
  private static readonly Color HighlightColor = new(1, 1, 1, 0.35f);
  private static readonly Color ShirtColor = new("f4f4f4");
  private static readonly Color TieColor = new("b3202a");
  private static readonly Color TrousersColor = new("4b5058");
  private static readonly Color CoatShirtColor = new("8fc3e8");
  private static readonly Color CoatSeamColor = new("8c9aa6");
  private static readonly Color PenColor = new("2a5bd7");
  private static readonly Color CapColor = new("d8231b");
  private static readonly Color CapDotColor = new("fbf6ea");
  private static readonly Color HornColor = new("140303");
  private static readonly Color HornEdgeColor = new("e8c8a8");
  private static readonly Color FoilColor = new("d9e2ea");
  private static readonly Color FoilEdgeColor = new("5b6b78");
  private static readonly Color AlienGreen = new("5fd35a");
  private static readonly Color SkinColor = new("e8c9a0");

  /// <summary>
  /// Часть тела на экране: «капсула» от <see cref="A"/> до <see cref="B"/> с радиусами концов; круг, если концы совпадают.
  /// </summary>
  private readonly record struct Part(Vector2 A, float RadiusA, Vector2 B, float RadiusB, Color Color, float Thick);

  /// <summary>
  /// Рука или нога: корень (плечо или бедро), сустав (локоть или колено), конец (кисть или щиколотка) и носок.
  /// </summary>
  private readonly record struct Limb(Vector3 Root, Vector3 Joint, Vector3 End, Vector3 Toe)
  {
    public float Depth => (Root.Z + Joint.Z + End.Z) / 3;
  }

  /// <summary>
  /// Положение суставов в текущей фазе шага. Индекс 0 — правая сторона тела, 1 — левая.
  /// </summary>
  private readonly record struct Skeleton(Vector3 Hip, Vector3 Shoulder, Vector3 Head, Limb[] Legs, Limb[] Arms);

  private float _phase;
  private float _gait;
  private float _height;
  private float _heading = 90;
  private Vector3 _forward;
  private Vector3 _right;

  /// <summary>
  /// Цвет человечка (тела, мантии, костюма или халата).
  /// </summary>
  public Color Color { get; init; } = MeepleView.Gray;

  /// <summary>
  /// Облик человечка — те же облики, что у <see cref="MeepleView"/>.
  /// </summary>
  public MeepleView.MeepleLook Look { get; init; } = MeepleView.MeepleLook.Wooden;

  /// <summary>
  /// Телосложение.
  /// </summary>
  public BodyBuild Build { get; init; } = BodyBuild.Chunky;

  /// <summary>
  /// Шагов в секунду.
  /// </summary>
  public float StepsPerSecond { get; set; } = 2.6f;

  /// <summary>
  /// Насколько человечек торопится: 0 — идёт, 1 — бежит со всех ног (сильнее наклон вперёд, размах рук и подъём колен).
  /// </summary>
  public float Haste { get; set; }

  /// <summary>
  /// Человечек шагает. Когда шаг выключают, он плавно останавливается и встаёт ровно.
  /// </summary>
  public bool Walking { get; set; }

  /// <summary>
  /// Куда смотрит человечек, в градусах по часовой стрелке: 0 — от зрителя (вверх по экрану, видно спину),
  /// 90 — вправо (профиль), 180 — на зрителя (анфас), 270 — влево; промежуточные — по диагонали.
  /// </summary>
  public float Heading
  {
    get => _heading;
    set
    {
      _heading = Mathf.Wrap(value, 0, 360);
      QueueRedraw();
    }
  }

  /// <summary>
  /// Фаза шага в радианах: полный цикл — два шага. Разная фаза у соседей, чтобы толпа не шагала в ногу.
  /// </summary>
  public float Phase
  {
    get => _phase;
    set
    {
      _phase = value;
      QueueRedraw();
    }
  }

  /// <summary>
  /// Высота над столом: человечек поднимается, а тень остаётся на месте.
  /// </summary>
  public float Height
  {
    get => _height;
    set
    {
      _height = value;
      QueueRedraw();
    }
  }

  /// <summary>
  /// Длина одного шага по столу: опорная ступня проходит под бёдрами от одного края размаха до другого.
  /// </summary>
  public float StepLength => 2 * (Body.Thigh + Body.Shin) * Mathf.Sin(LegSwing);

  /// <summary>
  /// Длина одного шага на экране в текущем направлении: шаги от зрителя и к зрителю на экране короче.
  /// </summary>
  public float ScreenStepLength
  {
    get
    {
      var angle = Mathf.DegToRad(_heading);
      var forward = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
      return StepLength * new Vector2(forward.X + forward.Z * DepthShear, forward.Z * DepthScale).Length();
    }
  }

  /// <summary>
  /// Сколько пикселей экрана человечек проходит за секунду, чтобы ступни не скользили по столу.
  /// </summary>
  public float WalkSpeed => ScreenStepLength * StepsPerSecond;

  private Proportions Body => Look == MeepleView.MeepleLook.Alien || Build == BodyBuild.Slim ? SlimBody : ChunkyBody;

  /// <summary>
  /// Повернуться по движению на экране: направление округляется до одного из восьми (через 45°).
  /// </summary>
  /// <param name="motion">Куда человечек идёт на экране.</param>
  public void FaceTowards(Vector2 motion)
  {
    // Обратная проекция на стол: вверх по экрану — от зрителя.
    var away = -motion.Y / DepthScale;
    var right = motion.X - away * DepthShear;
    if (Mathf.Abs(away) < 0.01f && Mathf.Abs(right) < 0.01f)
      return;
    var angle = Mathf.RadToDeg(Mathf.Atan2(right, away));
    Heading = Mathf.Round(angle / 45) * 45;
  }

  public override void _Process(double delta)
  {
    var target = Walking ? 1f : 0f;
    if (_gait == target && !Walking)
      return;

    _gait = Mathf.MoveToward(_gait, target, (float)delta * 4);
    if (Walking || _gait > 0)
      _phase = Mathf.Wrap(_phase + (float)delta * Mathf.Pi * StepsPerSecond, 0, Mathf.Tau);
    QueueRedraw();
  }

  public override void _Draw()
  {
    var angle = Mathf.DegToRad(_heading);
    _forward = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
    _right = new Vector3(Mathf.Cos(angle), 0, -Mathf.Sin(angle));

    var body = Body;
    var pose = Pose(body);

    // Тень сжимается, когда человечек поднят.
    var shadowScale = 1f / (1f + _height / 26f);
    DrawSetTransform(new Vector2(body.Thick * 0.3f, 0), 0, new Vector2(9.5f, 3.2f) * shadowScale);
    DrawCircle(Vector2.Zero, 1, ShadowColor);
    DrawSetTransform(Vector2.Zero);

    var top = Project(pose.Head).Y - body.Head * 2;
    if (Look == MeepleView.MeepleLook.Robed)
    {
      DrawRobed(body, pose, top);
      return;
    }

    var alien = Look == MeepleView.MeepleLook.Alien;
    var arm = body.Arm * (alien ? 0.8f : 1);
    var leg = body.Leg * (alien ? 0.75f : 1);
    var head = alien ? body.Head * 1.45f : body.Head;

    // Части тела от дальних к ближним. Ноги чуть «глубже» туловища: из-под него они видны, но не перекрывают его.
    var draws = new List<(float Depth, Action Draw)>();
    for (var side = 0; side < 2; side++)
    {
      var legs = pose.Legs[side];
      var arms = pose.Arms[side];
      var legColor = Shade(LegColor(side), legs.Depth);
      var armColor = Shade(ArmColor(side), arms.Depth);
      draws.Add((legs.Depth + 0.6f, () => DrawSolid(LegParts(legs, leg, legColor, body.Thick * 0.4f), top)));
      draws.Add((arms.Depth, () => DrawSolid(ArmParts(arms, arm, armColor, body.Thick * 0.35f), top)));
    }

    if (Look == MeepleView.MeepleLook.LabCoat)
      draws.Add((0.2f, () => DrawCoatTail(body, pose, top)));

    draws.Add((0, () =>
    {
      DrawSolid(
      [
        new Part(Project(pose.Hip), TorsoRadius(body.Waist, body.WaistWidth), Project(pose.Shoulder), TorsoRadius(body.Chest, body.ChestWidth), TorsoColor, body.Thick),
        new Part(Project(pose.Head), head, Project(pose.Head), head, HeadColor, 0),
      ], top);
      DrawTorsoFront(body, pose);
    }));

    foreach (var (_, draw) in draws.OrderByDescending(draw => draw.Depth))
      draw();

    // Блик на голове.
    var headCenter = Project(pose.Head);
    DrawCircle(headCenter + new Vector2(-head * 0.3f, -head * 0.45f), head * 0.22f, HighlightColor);
    DrawHeadwear(pose.Head, head);
  }

  private Color TorsoColor => Look == MeepleView.MeepleLook.Alien ? AlienGreen : Color;

  private Color HeadColor => Look switch
  {
    MeepleView.MeepleLook.Alien => AlienGreen,
    MeepleView.MeepleLook.Suit or MeepleView.MeepleLook.HalfSuit or MeepleView.MeepleLook.LabCoat => MeepleView.Gray,
    _ => Color,
  };

  /// <summary>
  /// Цвет ноги: <paramref name="side"/> 0 — правая, 1 — левая. У двойника в костюме только правая половина.
  /// </summary>
  private Color LegColor(int side) => Look switch
  {
    MeepleView.MeepleLook.Alien => AlienGreen,
    MeepleView.MeepleLook.HalfSuit when side == 1 => MeepleView.Gray,
    MeepleView.MeepleLook.LabCoat => TrousersColor,
    _ => Color,
  };

  private Color ArmColor(int side) => Look switch
  {
    MeepleView.MeepleLook.Alien => AlienGreen,
    MeepleView.MeepleLook.HalfSuit when side == 1 => MeepleView.Gray,
    _ => Color,
  };

  /// <summary>
  /// Дальние части тела темнее.
  /// </summary>
  private static Color Shade(Color color, float depth) => color.Darkened(Mathf.Clamp(depth, 0, 2.5f) * 0.08f);

  /// <summary>
  /// Точка в осях тела: <paramref name="forward"/> — вперёд, <paramref name="up"/> — вверх, <paramref name="right"/> — вправо.
  /// </summary>
  private Vector3 Body3(float forward, float up, float right) => _forward * forward + Vector3.Up * up + _right * right;

  /// <summary>
  /// Точка на экране: дальнее от зрителя (больше Z) — выше и чуть правее.
  /// </summary>
  private Vector2 Project(Vector3 point) =>
    new(point.X + point.Z * DepthShear, -point.Y - point.Z * DepthScale - _height);

  /// <summary>
  /// Видимая полуширина туловища: анфас — ширина, в профиль — толщина, между ними — эллипс.
  /// </summary>
  private float TorsoRadius(float depth, float width)
  {
    var visibleWidth = width * _right.X;
    var visibleDepth = depth * _forward.X;
    return Mathf.Sqrt(visibleWidth * visibleWidth + visibleDepth * visibleDepth);
  }

  private Skeleton Pose(Proportions body)
  {
    var legs = new[] { LegPose(body, _phase), LegPose(body, _phase + Mathf.Pi) };

    // Бёдра на такой высоте, чтобы нижняя из ступней стояла на столе.
    var footRadius = body.Leg * 0.85f;
    var hipUp = -Mathf.Min(legs[0].Lowest, legs[1].Lowest) + footRadius;
    var hip = Body3(0, hipUp, 0);

    var lean = Lean * (1 + _gait) + 0.22f * Haste * _gait;
    var shoulder = hip + Body3(Mathf.Sin(lean) * body.Torso, Mathf.Cos(lean) * body.Torso, 0);
    var headDistance = body.Head * (Look == MeepleView.MeepleLook.Alien ? 1.9f : 0.95f) + body.Chest * 0.35f;
    var head = shoulder + Body3(Mathf.Sin(lean * 0.5f) * headDistance, Mathf.Cos(lean * 0.5f) * headDistance, 0);

    // Руки идут навстречу ногам: правая рука — навстречу правой ноге.
    var swing = Mathf.Sin(_phase) * ArmSwing * (1 + 0.5f * Haste) * _gait;
    var armSwings = new[] { -swing, swing };
    var limbs = new Limb[2];
    var arms = new Limb[2];
    for (var i = 0; i < 2; i++)
    {
      var side = i == 0 ? 1f : -1f;
      var (knee, ankle, toe, _) = legs[i];
      var hipJoint = hip + Body3(0, 0, side * body.HipHalf);
      limbs[i] = new Limb(
        hipJoint,
        hipJoint + Body3(knee.X, knee.Y, 0),
        hipJoint + Body3(ankle.X, ankle.Y, 0),
        hipJoint + Body3(toe.X, toe.Y, 0));

      var armRoot = shoulder + Body3(0, -body.Arm * 0.6f, side * body.ShoulderHalf);
      var upper = armSwings[i];
      var fore = upper + ElbowBend + 0.15f * _gait;
      var elbow = armRoot + Body3(Mathf.Sin(upper) * body.UpperArm, -Mathf.Cos(upper) * body.UpperArm, side * 0.3f);
      var hand = elbow + Body3(Mathf.Sin(fore) * body.Forearm, -Mathf.Cos(fore) * body.Forearm, side * 0.3f);
      arms[i] = new Limb(armRoot, elbow, hand, hand);
    }

    return new Skeleton(hip, shoulder, head, limbs, arms);
  }

  /// <summary>
  /// Нога относительно бедра в плоскости «вперёд — вверх»: бедро качается, колено сгибается, пока нога идёт вперёд,
  /// носок опускается, когда нога сзади.
  /// </summary>
  private (Vector2 Knee, Vector2 Ankle, Vector2 Toe, float Lowest) LegPose(Proportions body, float phase)
  {
    var thigh = Mathf.Sin(phase) * LegSwing * _gait;
    var bend = Mathf.Pow(Mathf.Max(0, Mathf.Cos(phase)), 1.5f) * KneeBend * (1 + 0.35f * Haste) * _gait;
    var shin = thigh - bend;
    var knee = new Vector2(Mathf.Sin(thigh), -Mathf.Cos(thigh)) * body.Thigh;
    var ankle = knee + new Vector2(Mathf.Sin(shin), -Mathf.Cos(shin)) * body.Shin;
    var toeAngle = Mathf.Clamp(-shin * 0.8f, -0.25f, 1.1f);
    var toe = ankle + new Vector2(Mathf.Cos(toeAngle), -Mathf.Sin(toeAngle)) * body.Foot;
    return (knee, ankle, toe, Mathf.Min(ankle.Y, toe.Y));
  }

  private Part[] ArmParts(Limb arm, float radius, Color color, float thick) =>
  [
    new Part(Project(arm.Root), radius * 1.1f, Project(arm.Joint), radius, color, thick),
    new Part(Project(arm.Joint), radius, Project(arm.End), radius * 0.95f, color, thick),
    new Part(Project(arm.End), radius * 1.15f, Project(arm.End), radius * 1.15f, color, thick),
  ];

  private Part[] LegParts(Limb leg, float radius, Color color, float thick) =>
  [
    new Part(Project(leg.Root), radius * 1.15f, Project(leg.Joint), radius, color, thick),
    new Part(Project(leg.Joint), radius, Project(leg.End), radius * 0.9f, color, thick),
    new Part(Project(leg.End), radius * 0.85f, Project(leg.Toe), radius * 0.75f, color, thick),
  ];

  /// <summary>
  /// Объёмная группа частей: у коренастого сначала задний контур и толщина, потом у всех — передний контур и
  /// освещённые передние грани. Части одной группы сливаются без швов (колено, шея).
  /// </summary>
  private void DrawSolid(IReadOnlyList<Part> parts, float top)
  {
    var maxThick = parts.Max(part => part.Thick);
    foreach (var part in parts.Where(part => part.Thick > 0))
    {
      var outline = part.Color.Darkened(0.65f) with { A = 0.6f };
      DrawColoredPolygon(Offset(Shape(part, OutlineWidth), DepthDirection * part.Thick), outline);
    }

    for (var layer = maxThick; layer > 0; layer -= 1f)
    {
      foreach (var part in parts.Where(part => part.Thick >= layer))
      {
        var side = part.Color.Darkened(0.45f).Lerp(part.Color.Darkened(0.25f), 1 - layer / part.Thick);
        DrawColoredPolygon(Offset(Shape(part, 0), DepthDirection * layer), side);
      }
    }

    foreach (var part in parts)
      DrawColoredPolygon(Shape(part, OutlineWidth), part.Color.Darkened(0.65f));

    // Передние грани освещены сверху слева: края, обращённые к свету, светлее; сверху фигурка чуть светлее, чем снизу.
    var bottom = -_height;
    foreach (var part in parts)
    {
      var (shape, normals) = ShapeWithNormals(part, 0);
      var colors = new Color[shape.Length];
      for (var i = 0; i < shape.Length; i++)
      {
        var lit = normals[i].Dot(LightDirection);
        var color = lit > 0 ? part.Color.Lightened(0.35f * lit) : part.Color.Darkened(0.3f * -lit);
        colors[i] = color.Darkened(0.12f * Mathf.Clamp((shape[i].Y - top) / (bottom - top), 0, 1));
      }

      DrawPolygon(shape, colors);
    }
  }

  private static Vector2[] Offset(Vector2[] points, Vector2 offset) => points.Select(point => point + offset).ToArray();

  private static Vector2[] Shape(Part part, float extra) => ShapeWithNormals(part, extra).Points;

  /// <summary>
  /// Контур части (две полуокружности, соединённые касательными; у круга — окружность) и нормали в его точках —
  /// по ним часть освещается как цилиндр или шар.
  /// </summary>
  private static (Vector2[] Points, Vector2[] Normals) ShapeWithNormals(Part part, float extra)
  {
    var ra = part.RadiusA + extra;
    var rb = part.RadiusB + extra;
    if (part.A.DistanceSquaredTo(part.B) < 0.01f)
    {
      var around = Enumerable.Range(0, ArcSegments * 2).Select(i => Vector2.FromAngle(Mathf.Tau * i / (ArcSegments * 2))).ToArray();
      return (around.Select(normal => part.A + normal * Mathf.Max(ra, rb)).ToArray(), around);
    }

    var direction = (part.B - part.A).Normalized();
    var side = direction.Orthogonal();
    var points = new Vector2[(ArcSegments + 1) * 2];
    var normals = new Vector2[points.Length];
    for (var i = 0; i <= ArcSegments; i++)
    {
      var angle = -Mathf.Pi / 2 + Mathf.Pi * i / ArcSegments;
      normals[i] = direction * Mathf.Cos(angle) + side * Mathf.Sin(angle);
      normals[ArcSegments + 1 + i] = -normals[i];
      points[i] = part.B + normals[i] * rb;
      points[ArcSegments + 1 + i] = part.A + normals[ArcSegments + 1 + i] * ra;
    }

    return (points, normals);
  }

  /// <summary>
  /// Грудь видна, если человечек не повёрнут к зрителю спиной (в профиль — видна её кромка).
  /// </summary>
  private bool ChestVisible => _forward.Z < 0.45f;

  /// <summary>
  /// Точка на груди: <paramref name="along"/> — доля от бёдер (0) к плечам (1), <paramref name="inset"/> — насколько
  /// утоплена внутрь (заметно в профиль), <paramref name="across"/> — смещение вправо в долях полуширины (заметно анфас).
  /// </summary>
  private Vector2 Chest(Proportions body, Skeleton pose, float along, float inset, float across)
  {
    var center = pose.Hip.Lerp(pose.Shoulder, along);
    var depth = Mathf.Lerp(body.Waist, body.Chest, along);
    var width = Mathf.Lerp(body.WaistWidth, body.ChestWidth, along);
    return Project(center + _forward * (depth - inset) + _right * (across * width));
  }

  /// <summary>
  /// Костюм, половинный костюм и халат: воротник, галстук, вырез, пуговицы, карман.
  /// </summary>
  private void DrawTorsoFront(Proportions body, Skeleton pose)
  {
    if (!ChestVisible)
      return;

    Vector2 At(float along, float inset, float across) => Chest(body, pose, along, inset, across);

    switch (Look)
    {
      case MeepleView.MeepleLook.Suit or MeepleView.MeepleLook.HalfSuit:
      {
        var left = Look == MeepleView.MeepleLook.HalfSuit ? 0 : -0.5f;
        DrawColoredPolygon([At(1.02f, 0.2f, 0.5f), At(1.02f, 1.9f, left), At(0.72f, 0.3f, 0)], ShirtColor);
        DrawColoredPolygon(
        [
          At(0.98f, 0.3f, 0.12f), At(0.98f, 1f, -0.12f), At(0.45f, 1.1f, -0.16f), At(0.35f, 0.2f, 0), At(0.5f, -0.1f, 0.16f),
        ], TieColor);
        break;
      }
      case MeepleView.MeepleLook.LabCoat:
      {
        DrawColoredPolygon([At(1.02f, 0.2f, 0.45f), At(1.02f, 1.6f, -0.45f), At(0.7f, 0.3f, 0)], CoatShirtColor);
        DrawPolyline([At(1f, 1.9f, -0.55f), At(0.7f, 0.5f, 0), At(-0.1f, 0.5f, 0)], CoatSeamColor, 0.6f, antialiased: true);
        DrawLine(At(1f, 0.2f, 0.55f), At(0.7f, 0.5f, 0), CoatSeamColor, 0.6f, antialiased: true);
        foreach (var along in new[] { 0.5f, 0.25f })
          DrawCircle(At(along, 1f, 0.08f), body.Chest * 0.12f, CoatSeamColor);
        DrawLine(At(0.92f, 1.5f, -0.5f), At(0.72f, 1.5f, -0.5f), PenColor, 0.9f);
        break;
      }
    }
  }

  /// <summary>
  /// Пола халата: свисает с бёдер до середины бедра и чуть отстаёт при ходьбе.
  /// </summary>
  private void DrawCoatTail(Proportions body, Skeleton pose, float top)
  {
    var sway = Mathf.Sin(_phase * 2) * 0.4f * _gait - 0.8f * _gait;
    var hem = pose.Hip + Body3(sway - body.Waist * 0.2f, -body.Thigh * 0.85f, 0);
    var radius = TorsoRadius(body.Waist, body.WaistWidth);
    DrawSolid([new Part(Project(pose.Hip), radius, Project(hem), radius * 1.15f, Color, body.Thick * 0.9f)], top);
  }

  /// <summary>
  /// Человек в мантии: колокол мантии скрывает ноги (видны только носки), капюшон с лицом в тени, широкие рукава.
  /// </summary>
  private void DrawRobed(Proportions body, Skeleton pose, float top)
  {
    var shoe = Color.Darkened(0.55f);
    var draws = new List<(float Depth, Action Draw)>();
    for (var side = 0; side < 2; side++)
    {
      var leg = pose.Legs[side];
      var arm = pose.Arms[side];
      var armColor = Shade(Color, arm.Depth);
      draws.Add((leg.Depth + 5, () => DrawSolid([new Part(Project(leg.End), body.Leg * 0.8f, Project(leg.Toe), body.Leg * 0.7f, Shade(shoe, leg.Depth), body.Thick * 0.4f)], top)));
      draws.Add((arm.Depth, () =>
      {
        DrawSolid(ArmParts(arm, body.Arm * 1.25f, armColor, body.Thick * 0.35f), top);
        DrawCircle(Project(arm.End) + new Vector2(0, body.Arm * 0.8f), body.Arm * 0.7f, SkinColor);
      }));
    }

    draws.Add((0, () =>
    {
      // Подол качается в такт шагу.
      var sway = Mathf.Sin(_phase * 2) * 0.5f * _gait - 0.6f * _gait;
      var hem = Body3(sway, body.Leg * 1.3f, 0);
      var head = pose.Head;
      var hood = body.Head;
      DrawSolid(
      [
        new Part(Project(pose.Shoulder), TorsoRadius(body.Chest, body.ChestWidth) * 0.95f, Project(hem), TorsoRadius(body.Thigh, body.Thigh * 1.15f) * 1.05f, Color, body.Thick),
        new Part(Project(head), hood * 1.05f, Project(head), hood * 1.05f, Color, 0),
        new Part(Project(head + Body3(-hood * 0.15f, hood * 0.6f, 0)), hood * 0.55f, Project(head + Body3(-hood * 0.5f, hood * 1.25f, 0)), 0.2f, Color, body.Thick),
      ], top);

      // Лицо глубоко в капюшоне; со спины его не видно.
      if (_forward.Z < 0.5f)
      {
        var face = Project(head + Body3(hood * 0.45f, -hood * 0.1f, 0));
        var inward = Project(head + Body3(hood * 0.55f, -hood * 0.12f, 0)) - face;
        DrawCircle(face, hood * 0.55f, Color.Darkened(0.35f));
        DrawCircle(face + inward, hood * 0.38f, SkinColor);
      }
    }));

    foreach (var (_, draw) in draws.OrderByDescending(draw => draw.Depth))
      draw();
  }

  /// <summary>
  /// Шляпка мухомора, рожки, шапочка из фольги и глаза инопланетянина.
  /// </summary>
  private void DrawHeadwear(Vector3 head, float radius)
  {
    var center = Project(head);
    Vector2 Flat(float x, float y) => center + new Vector2(x * radius, y * radius);
    Vector2 On(float forward, float up, float right) => Project(head + Body3(forward * radius, up * radius, right * radius));

    switch (Look)
    {
      case MeepleView.MeepleLook.Alien:
      {
        // Огромные чёрные миндалевидные глаза на лице; тот, что смотрит от зрителя, не виден.
        foreach (var side in new[] { 1f, -1f }.OrderByDescending(side => (_forward * 0.8f + _right * side * 0.6f).Z))
        {
          var facing = (_forward * 0.8f + _right * side * 0.6f).Normalized();
          if (facing.Z > 0.15f)
            continue;
          var eye = On(0.75f, 0.05f, side * 0.45f);
          var squeeze = Mathf.Clamp(-facing.Z + 0.35f, 0.35f, 1f);
          var tilt = side * Mathf.Sign(_right.X) * 0.35f * squeeze;
          var shape = Enumerable.Range(0, 16)
            .Select(i => Mathf.Tau * i / 16)
            .Select(a => new Vector2(Mathf.Cos(a) * 0.36f * squeeze, Mathf.Sin(a) * 0.22f).Rotated(tilt) * radius + eye)
            .ToArray();
          DrawColoredPolygon(shape, Colors.Black);
          DrawCircle(eye + new Vector2(-0.08f, -0.06f) * radius, radius * 0.07f, HighlightColor);
        }

        break;
      }
      case MeepleView.MeepleLook.Mushroom:
      {
        var cap = new List<Vector2>();
        for (var i = 0; i <= 16; i++)
        {
          var angle = Mathf.Pi + Mathf.Pi * i / 16;
          cap.Add(Flat(Mathf.Cos(angle) * 1.55f, -0.25f + Mathf.Sin(angle) * 1.05f));
        }

        cap.Add(Flat(0.7f, -0.05f));
        cap.Add(Flat(-0.7f, -0.05f));
        var points = cap.ToArray();
        DrawColoredPolygon(Offset(points, new Vector2(DepthShear, -DepthScale) * radius * 0.6f), CapColor.Darkened(0.45f));
        DrawColoredPolygon(points, CapColor);
        DrawPolyline([.. points, points[0]], CapColor.Darkened(0.5f), 0.6f, antialiased: true);
        foreach (var (x, y, r) in new[] { (-0.8f, -0.55f, 0.22f), (0.15f, -1.0f, 0.26f), (0.9f, -0.5f, 0.2f), (-0.25f, -0.4f, 0.15f) })
          DrawCircle(Flat(x, y), radius * r, CapDotColor);
        break;
      }
      case MeepleView.MeepleLook.Horned:
      {
        // Рожки по бокам макушки, загнуты вперёд и чуть в стороны; дальний темнее.
        foreach (var side in new[] { 1f, -1f }.OrderByDescending(side => (_right * side).Z))
        {
          Vector2[] horn =
          [
            On(-0.15f, 0.8f, side * 0.25f), On(0.3f, 0.75f, side * 0.35f), On(0.55f, 1.25f, side * 0.5f),
            On(0.5f, 1.85f, side * 0.6f), On(0.2f, 1.25f, side * 0.4f),
          ];
          var color = (_right * side).Z > 0.1f ? HornColor.Lightened(0.15f) : HornColor;
          DrawColoredPolygon(horn, color);
          DrawPolyline([.. horn, horn[0]], HornEdgeColor, 0.4f, antialiased: true);
        }

        break;
      }
      case MeepleView.MeepleLook.FoilHat:
      {
        Vector2[] cone = [Flat(-0.85f, -0.55f), Flat(-0.05f, -2.3f), Flat(0.85f, -0.55f)];
        DrawColoredPolygon(Offset(cone, new Vector2(DepthShear, -DepthScale) * radius * 0.5f), FoilEdgeColor);
        DrawColoredPolygon(cone, FoilColor);
        DrawPolyline([.. cone, cone[0]], FoilEdgeColor, 0.6f, antialiased: true);
        Vector2[] brim = [Flat(-1.25f, -0.62f), Flat(1.25f, -0.62f), Flat(1.25f, -0.38f), Flat(-1.25f, -0.38f)];
        DrawColoredPolygon(brim, FoilColor);
        DrawPolyline([.. brim, brim[0]], FoilEdgeColor, 0.6f, antialiased: true);
        DrawLine(Flat(-0.2f, -0.9f), Flat(0.15f, -1.5f), FoilEdgeColor, 0.5f, antialiased: true);
        DrawLine(Flat(0.25f, -0.7f), Flat(-0.05f, -1.2f), FoilEdgeColor, 0.5f, antialiased: true);
        break;
      }
    }
  }
}
