using Godot;

namespace ConspiracyLeague.Game.Cards;

/// <summary>
/// Шагающий человечек-адепт в 2.5D. Может идти в любую сторону: в профиль, анфас, спиной и по диагоналям.
/// Начало координат узла — точка, где человечек стоит на столе (под бёдрами).
/// </summary>
/// <remarks>
/// <para>
/// Скелет объёмный: бёдра и плечи разнесены в стороны, суставы считаются в осях тела (вперёд, вверх, вправо)
/// и проецируются на экран: стол виден сверху под углом, дальнее — выше и чуть правее. Части тела рисуются
/// от дальних к ближним. Походка считается по фазе шага: опорная нога прямая, маховая сгибается в колене,
/// руки идут навстречу ногам, а бёдра поднимаются так, чтобы опорная ступня всегда стояла на столе.
/// </para>
/// <para>
/// Человечек одет: рубашка цвета <see cref="Color"/>, брюки темнее, ботинки; лицо, шея и кисти — цвета
/// <see cref="SkinTone"/>, волосы — <see cref="HairColor"/>. Пришелец — зелёный и без одежды, человек в мантии —
/// в длинной мантии с капюшоном.
/// </para>
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

    /// <summary>Стройный и мультяшный: крупная голова с большими глазами, короткие ручки и ножки; объём — светом и тенью.</summary>
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

  /// <summary>
  /// Стройный, но мультяшный: крупная голова (около трети роста), короткие ножки и ручки, круглое туловище.
  /// </summary>
  private static readonly Proportions SlimBody = new(
    Thigh: 4.2f, Shin: 3.9f, Leg: 1.5f, Foot: 2.2f, Torso: 6.4f, Chest: 2.9f, Waist: 2.6f, ChestWidth: 3.4f, WaistWidth: 3.2f,
    HipHalf: 1.4f, ShoulderHalf: 2.9f, Head: 4f, UpperArm: 3.4f, Forearm: 3.2f, Arm: 1.15f, Thick: 0f);

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

  /// <summary>
  /// Обычный цвет кожи.
  /// </summary>
  public static readonly Color DefaultSkin = new("e8c9a0");

  /// <summary>
  /// Обычный цвет волос.
  /// </summary>
  public static readonly Color DefaultHair = new("4a3426");

  private static readonly Color ShadowColor = new(0, 0, 0, 0.35f);
  private static readonly Color HighlightColor = new(1, 1, 1, 0.3f);
  private static readonly Color EyeColor = new("2a1d17");
  private static readonly Color EyeShineColor = new(1, 1, 1, 0.9f);
  private static readonly Color BlushColor = new(1f, 0.45f, 0.45f, 0.3f);
  private static readonly Color MouthColor = new("5a2e22");
  private static readonly Color ShoeColor = new("2b2420");
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
  private static readonly Color RobeLegsColor = new("2a2630");
  private static readonly Color RobeTrimColor = new("9c7d3c");
  private static readonly Color HoodOpeningColor = new("0b0a0e");

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
  /// Цвет человечка: рубашки и рукавов (брюки — темнее), мантии, костюма или халата.
  /// </summary>
  public Color Color { get; init; } = MeepleView.Gray;

  /// <summary>
  /// Цвет кожи: лицо, шея, кисти.
  /// </summary>
  public Color SkinTone { get; init; } = DefaultSkin;

  /// <summary>
  /// Цвет волос.
  /// </summary>
  public Color HairColor { get; init; } = DefaultHair;

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

  private bool Alien => Look == MeepleView.MeepleLook.Alien;

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
    var robed = Look == MeepleView.MeepleLook.Robed;

    // Тень сжимается, когда человечек поднят.
    var shadowScale = 1f / (1f + _height / 26f);
    var shadow = robed ? new Vector2(9f, 3.2f) : new Vector2(7.5f, 2.7f);
    DrawSetTransform(new Vector2(body.Thick * 0.3f, 0), 0, shadow * shadowScale);
    DrawCircle(Vector2.Zero, 1, ShadowColor);
    DrawSetTransform(Vector2.Zero);

    var top = Project(pose.Head).Y - body.Head * 2;
    if (robed)
    {
      DrawRobed(body, pose, top);
      return;
    }

    var arm = body.Arm * (Alien ? 0.8f : 1);
    var leg = body.Leg * (Alien ? 0.75f : 1);
    var head = HeadRadius(body);

    // Части тела от дальних к ближним. Ноги чуть «глубже» туловища: из-под него они видны, но не перекрывают его.
    var draws = new List<(float Depth, Action Draw)>();
    for (var side = 0; side < 2; side++)
    {
      var legs = pose.Legs[side];
      var arms = pose.Arms[side];
      var trousers = Shade(TrousersOf(side), legs.Depth);
      var shoes = Shade(Alien ? AlienGreen : ShoeColor, legs.Depth);
      var sleeve = Shade(SleeveOf(side), arms.Depth);
      var hand = Shade(Alien ? AlienGreen : SkinTone, arms.Depth);
      draws.Add((legs.Depth + 0.6f, () => DrawSolid(LegParts(legs, leg, trousers, shoes, body.Thick * 0.4f), top)));
      draws.Add((arms.Depth, () => DrawSolid(ArmParts(arms, arm, sleeve, hand, body.Thick * 0.35f), top)));
    }

    if (Look == MeepleView.MeepleLook.LabCoat)
      draws.Add((0.2f, () => DrawCoatTail(body, pose, top)));

    draws.Add((0, () =>
    {
      DrawSolid(TorsoParts(body, pose, head), top);
      if (!Alien)
        DrawFace(pose.Head, head);
      DrawTorsoFront(body, pose);
    }));

    foreach (var (_, draw) in draws.OrderByDescending(draw => draw.Depth))
      draw();

    // Блик на голове.
    var headCenter = Project(pose.Head);
    DrawCircle(headCenter + new Vector2(-head * 0.3f, -head * 0.45f), head * 0.2f, HighlightColor);
    DrawHeadwear(pose.Head, head);
  }

  private float HeadRadius(Proportions body) => Alien ? body.Head * 1.3f : body.Head;

  /// <summary>
  /// Туловище в рубашке, пояс брюк, шея и голова (у людей — цвета волос: лицо рисуется поверх).
  /// </summary>
  private List<Part> TorsoParts(Proportions body, Skeleton pose, float head)
  {
    var shirt = Alien ? AlienGreen : Color;
    var skin = Alien ? AlienGreen : SkinTone;
    var chest = TorsoRadius(body.Chest, body.ChestWidth);
    var waist = TorsoRadius(body.Waist, body.WaistWidth);
    var axis = (pose.Shoulder - pose.Hip).Normalized();

    // Верх туловища опущен, чтобы плечи были покатыми, а над ними была видна шея.
    var shoulders = pose.Shoulder - axis * chest * 0.5f;
    var parts = new List<Part> { new(Project(pose.Hip), waist, Project(shoulders), chest, shirt, body.Thick) };

    // Брюки до пояса; у халата их закрывают полы.
    if (!Alien && Look != MeepleView.MeepleLook.LabCoat)
    {
      var belt = pose.Hip + axis * body.Torso * 0.22f;
      parts.Add(new Part(Project(pose.Hip), waist * 1.08f, Project(belt), waist * 1.02f, TrousersOf(0), body.Thick));
    }

    var neckBase = pose.Shoulder + axis * chest * 0.25f;
    parts.Add(new Part(Project(neckBase), head * (Alien ? 0.18f : 0.32f), Project(pose.Head), head * (Alien ? 0.18f : 0.3f), skin, 0));
    parts.Add(new Part(Project(pose.Head), head, Project(pose.Head), head, Alien ? AlienGreen : HairColor, 0));
    return parts;
  }

  /// <summary>
  /// Брюки: <paramref name="side"/> 0 — правая нога, 1 — левая. У двойника в костюме только правая половина.
  /// </summary>
  private Color TrousersOf(int side) => Look switch
  {
    MeepleView.MeepleLook.Alien => AlienGreen,
    MeepleView.MeepleLook.Suit => Color,
    MeepleView.MeepleLook.HalfSuit => side == 0 ? Color : MeepleView.Gray.Darkened(0.35f),
    MeepleView.MeepleLook.LabCoat => TrousersColor,
    _ => Color.Darkened(0.35f),
  };

  private Color SleeveOf(int side) => Look switch
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

    // Голова над покатыми плечами, между ними — шея.
    var head = HeadRadius(body);
    var headDistance = Mathf.Max(body.Chest, body.ChestWidth) * 0.75f + head * 0.9f;
    var headCenter = shoulder + Body3(Mathf.Sin(lean * 0.5f) * headDistance, Mathf.Cos(lean * 0.5f) * headDistance, 0);

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

      var armRoot = shoulder + Body3(0, -body.Arm * 0.9f, side * body.ShoulderHalf);
      var upper = armSwings[i];
      var fore = upper + ElbowBend + 0.15f * _gait;
      var elbow = armRoot + Body3(Mathf.Sin(upper) * body.UpperArm, -Mathf.Cos(upper) * body.UpperArm, side * 0.3f);
      var hand = elbow + Body3(Mathf.Sin(fore) * body.Forearm, -Mathf.Cos(fore) * body.Forearm, side * 0.2f);
      arms[i] = new Limb(armRoot, elbow, hand, hand);
    }

    return new Skeleton(hip, shoulder, headCenter, limbs, arms);
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

  /// <summary>
  /// Рука: плечо и предплечье в рукаве, кисть чуть дальше запястья.
  /// </summary>
  private Part[] ArmParts(Limb arm, float radius, Color sleeve, Color hand, float thick)
  {
    var wrist = Project(arm.End);
    var along = (wrist - Project(arm.Joint)).Normalized();
    return
    [
      new Part(Project(arm.Root), radius * 1.15f, Project(arm.Joint), radius, sleeve, thick),
      new Part(Project(arm.Joint), radius, wrist, radius * 0.95f, sleeve, thick),
      new Part(wrist + along * radius * 0.8f, radius * 1.05f, wrist + along * radius * 0.8f, radius * 1.05f, hand, thick),
    ];
  }

  /// <summary>
  /// Нога: бедро и голень в брюках, ступня — ботинок.
  /// </summary>
  private Part[] LegParts(Limb leg, float radius, Color trousers, Color shoes, float thick) =>
  [
    new Part(Project(leg.Root), radius * 1.2f, Project(leg.Joint), radius, trousers, thick),
    new Part(Project(leg.Joint), radius, Project(leg.End), radius * 0.85f, trousers, thick),
    new Part(Project(leg.End), radius * 1f, Project(leg.Toe), radius * 0.95f, shoes, thick),
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

    foreach (var part in parts)
    {
      var (shape, normals) = ShapeWithNormals(part, 0);
      DrawPolygon(shape, Lit(shape, normals, part.Color, top, 0.35f));
    }
  }

  /// <summary>
  /// Цвета точек контура при свете сверху слева: края, обращённые к свету, светлее; сверху фигурка чуть светлее, чем снизу.
  /// </summary>
  /// <param name="lift">Насколько светлеют освещённые края (тёмной ткани нужно больше, иначе объём не виден).</param>
  /// <param name="dark">Насколько темнеют края в тени.</param>
  private Color[] Lit(Vector2[] shape, Vector2[] normals, Color color, float top, float lift, float dark = 0.3f)
  {
    var bottom = -_height;
    var colors = new Color[shape.Length];
    for (var i = 0; i < shape.Length; i++)
    {
      var lit = normals[i].Dot(LightDirection);
      var shaded = lit > 0 ? color.Lightened(lift * lit) : color.Darkened(dark * -lit);
      colors[i] = shaded.Darkened(0.12f * Mathf.Clamp((shape[i].Y - top) / (bottom - top), 0, 1));
    }

    return colors;
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
  /// Лицо поверх головы цвета волос: там, куда человечек смотрит, и чуть ниже — волосы остаются на макушке и затылке.
  /// Спиной к зрителю лица не видно. Свет на лице мягкий: резкие блики и тени делают его восковым.
  /// </summary>
  private void DrawFace(Vector3 head, float radius)
  {
    if (_forward.Z > 0.25f)
      return;

    var center = Project(head);
    var toward = Project(head + _forward * radius) - center;
    var faceCenter = center + toward * 0.4f + new Vector2(0, radius * 0.2f);
    var face = Lens(center, radius, faceCenter, radius * 0.88f);
    if (face.Length < 3)
      return;

    var normals = face.Select(point => (point - center) / radius).ToArray();
    var top = center.Y - radius * 2;
    DrawPolygon(face, Lit(face, normals, SkinTone, top, 0.12f, 0.1f));
    DrawFaceFeatures(head, radius, full: true);
  }

  /// <summary>
  /// Большие блестящие глаза, а при <paramref name="full"/> — ещё румянец и улыбка; только то, что обращено к зрителю.
  /// </summary>
  private void DrawFaceFeatures(Vector3 head, float radius, bool full)
  {
    if (full)
    {
      foreach (var side in new[] { 1f, -1f })
      {
        var cheek = (_forward * 0.7f + _right * side * 0.55f + Vector3.Down * 0.4f).Normalized();
        if (cheek.Z < -0.1f)
          DrawCircle(Project(head + cheek * radius * 0.9f), radius * 0.17f, BlushColor);
      }
    }

    foreach (var side in new[] { 1f, -1f })
    {
      var look = (_forward * 0.84f + _right * side * 0.4f + Vector3.Down * 0.05f).Normalized();
      if (look.Z > -0.05f)
        continue;
      var eye = Project(head + look * radius * 0.93f);
      var squeeze = Mathf.Clamp(-look.Z * 1.3f, 0.35f, 1f);
      DrawColoredPolygon(Ellipse(eye, radius * 0.2f * squeeze, radius * 0.26f), EyeColor);
      DrawCircle(eye + new Vector2(-0.06f, -0.1f) * radius, radius * 0.09f, EyeShineColor);
    }

    var mouth = (_forward * 0.9f + Vector3.Down * 0.42f).Normalized();
    if (!full || mouth.Z > 0.05f)
      return;
    var width = radius * 0.16f * Mathf.Clamp(-mouth.Z * 1.3f, 0.4f, 1f);
    var at = Project(head + mouth * radius * 0.95f) - new Vector2(0, width * 0.5f);
    DrawArc(at, width, Mathf.Pi * 0.15f, Mathf.Pi * 0.85f, 8, MouthColor, radius * 0.07f, antialiased: true);
  }

  private static Vector2[] Ellipse(Vector2 center, float rx, float ry) =>
    Enumerable.Range(0, 14).Select(i => Mathf.Tau * i / 14).Select(a => center + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry)).ToArray();

  /// <summary>
  /// Пересечение двух кругов — выпуклый многоугольник.
  /// </summary>
  private static Vector2[] Lens(Vector2 a, float ra, Vector2 b, float rb)
  {
    const int segments = 28;
    var points = new List<Vector2>();
    for (var i = 0; i < segments; i++)
    {
      var direction = Vector2.FromAngle(Mathf.Tau * i / segments);
      var onA = a + direction * ra;
      if (onA.DistanceTo(b) <= rb)
        points.Add(onA);
      var onB = b + direction * rb;
      if (onB.DistanceTo(a) <= ra)
        points.Add(onB);
    }

    if (points.Count < 3)
      return [];
    var middle = points.Aggregate((x, y) => x + y) / points.Count;
    return points.OrderBy(point => (point - middle).Angle()).ToArray();
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
        var left = Look == MeepleView.MeepleLook.HalfSuit ? 0 : -0.45f;
        DrawColoredPolygon([At(0.98f, 0.2f, 0.45f), At(0.98f, 1.6f, left), At(0.7f, 0.3f, 0)], ShirtColor);
        DrawColoredPolygon(
        [
          At(0.94f, 0.3f, 0.1f), At(0.94f, 0.9f, -0.1f), At(0.45f, 1f, -0.14f), At(0.36f, 0.2f, 0), At(0.5f, -0.1f, 0.14f),
        ], TieColor);
        break;
      }
      case MeepleView.MeepleLook.LabCoat:
      {
        DrawColoredPolygon([At(0.98f, 0.2f, 0.42f), At(0.98f, 1.4f, -0.42f), At(0.7f, 0.3f, 0)], CoatShirtColor);
        DrawPolyline([At(0.96f, 1.7f, -0.5f), At(0.7f, 0.5f, 0), At(-0.1f, 0.5f, 0)], CoatSeamColor, 0.5f, antialiased: true);
        DrawLine(At(0.96f, 0.2f, 0.5f), At(0.7f, 0.5f, 0), CoatSeamColor, 0.5f, antialiased: true);
        foreach (var along in new[] { 0.5f, 0.25f })
          DrawCircle(At(along, 1f, 0.08f), body.Chest * 0.1f, CoatSeamColor);
        DrawLine(At(0.88f, 1.4f, -0.5f), At(0.7f, 1.4f, -0.5f), PenColor, 0.8f);
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
    DrawSolid([new Part(Project(pose.Hip), radius * 1.05f, Project(hem), radius * 1.2f, Color, body.Thick * 0.9f)], top);
  }

  /// <summary>
  /// Человек в мантии: длинная мантия колоколом от плеч до щиколоток (из-под подола видны щиколотки и ботинки),
  /// капюшон с накидкой на плечах и лицом в тени, широкие рукава, пояс-шнур. Края тёмной ткани подсвечены,
  /// чтобы фигура читалась на тёмном столе.
  /// </summary>
  private void DrawRobed(Proportions body, Skeleton pose, float top)
  {
    var draws = new List<(float Depth, Action Draw)>();
    for (var side = 0; side < 2; side++)
    {
      var leg = pose.Legs[side];
      var arm = pose.Arms[side];
      var legs = Shade(RobeLegsColor, leg.Depth);
      var shoes = Shade(ShoeColor, leg.Depth);
      var sleeve = Shade(Color, arm.Depth);
      var hand = Shade(SkinTone, arm.Depth);
      draws.Add((leg.Depth + 10, () => DrawSolid(LegParts(leg, body.Leg, legs, shoes, 0), top)));
      draws.Add((arm.Depth, () => DrawSolid(SleeveParts(arm, body.Arm, sleeve, hand), top)));
    }

    draws.Add((0, () =>
    {
      DrawRobe(body, pose, top);
      DrawHood(body, pose, top);
    }));

    foreach (var (_, draw) in draws.OrderByDescending(draw => draw.Depth))
      draw();
  }

  /// <summary>
  /// Широкий рукав мантии, расширяющийся к запястью; из него выглядывает кисть.
  /// </summary>
  private Part[] SleeveParts(Limb arm, float radius, Color sleeve, Color hand)
  {
    var wrist = Project(arm.End);
    var along = (wrist - Project(arm.Joint)).Normalized();
    return
    [
      new Part(wrist + along * radius * 1f, radius * 0.85f, wrist + along * radius * 1f, radius * 0.85f, hand, 0),
      new Part(Project(arm.Root), radius * 1.1f, Project(arm.Joint), radius * 1.2f, sleeve, 0),
      new Part(Project(arm.Joint), radius * 1.2f, wrist, radius * 1.55f, sleeve, 0),
    ];
  }

  /// <summary>
  /// Мантия: выпуклая оболочка плеч и подола. Подол — эллипс на высоте щиколоток, он отстаёт и качается при ходьбе.
  /// По видимой стороне — складки, отделка подола и пояс-шнур.
  /// </summary>
  private void DrawRobe(Proportions body, Skeleton pose, float top)
  {
    var sway = Mathf.Sin(_phase * 2) * 0.4f * _gait - 0.5f * _gait;
    var hemCenter = Body3(sway, body.Leg * 2.8f, 0);
    var hemWidth = body.ChestWidth * 1.45f;
    var hemDepth = body.Chest * 1.45f;
    var shoulders = Project(pose.Shoulder);
    var shoulderRadius = TorsoRadius(body.Chest * 1.1f, body.ChestWidth * 1.1f);

    Vector3 Around(Vector3 center, float angle, float width, float depth) =>
      center + _right * (Mathf.Cos(angle) * width) + _forward * (Mathf.Sin(angle) * depth);

    var points = new List<Vector2>();
    for (var i = 0; i <= 12; i++)
      points.Add(shoulders + Vector2.FromAngle(Mathf.Pi + Mathf.Pi * i / 12) * shoulderRadius);
    for (var i = 0; i < 32; i++)
      points.Add(Project(Around(hemCenter, Mathf.Tau * i / 32, hemWidth, hemDepth)));

    var hull = ConvexHull(points);
    var hem = Project(hemCenter);
    Vector2 NormalOf(Vector2 point)
    {
      var t = Mathf.Clamp((point.Y - shoulders.Y) / Mathf.Max(0.01f, hem.Y - shoulders.Y), 0, 1);
      var offset = point - shoulders.Lerp(hem, t);
      return offset.LengthSquared() < 0.001f ? Vector2.Up : offset.Normalized();
    }

    DrawPolygon(hull, Lit(hull, hull.Select(NormalOf).ToArray(), Color, top, 0.45f));
    DrawPolyline([.. hull, hull[0]], Color.Lightened(0.22f) with { A = 0.55f }, 0.5f, antialiased: true);

    // Видимая (обращённая к зрителю) сторона эллипса — там складки, отделка подола и шнур пояса.
    var facing = Mathf.Atan2(-_forward.Z * hemDepth, -_right.Z * hemWidth);
    var waist = pose.Hip + (pose.Shoulder - pose.Hip) * 0.3f;
    var fold = Color.Lightened(0.14f);
    foreach (var shift in new[] { -0.75f, 0f, 0.75f })
    {
      var angle = facing + shift;
      DrawLine(
        Project(Around(waist, angle, body.WaistWidth * 0.9f, body.Waist * 0.9f) + Vector3.Down * body.Torso * 0.15f),
        Project(Around(hemCenter, angle, hemWidth * 0.95f, hemDepth * 0.95f)),
        fold, 0.35f, antialiased: true);
    }

    DrawVisibleArc(hemCenter, hemWidth, hemDepth, RobeTrimColor, 0.5f);
    DrawVisibleArc(waist, body.WaistWidth * 1.05f, body.Waist * 1.05f, RobeTrimColor, 0.45f);
  }

  /// <summary>
  /// Обращённая к зрителю половина эллипса вокруг <paramref name="center"/> — линией.
  /// </summary>
  private void DrawVisibleArc(Vector3 center, float width, float depth, Color color, float lineWidth)
  {
    const int segments = 32;
    Vector3 At(int i)
    {
      var angle = Mathf.Tau * i / segments;
      return _right * (Mathf.Cos(angle) * width) + _forward * (Mathf.Sin(angle) * depth);
    }

    for (var i = 0; i < segments; i++)
    {
      var (a, b) = (At(i), At(i + 1));
      if (a.Z <= 0.05f && b.Z <= 0.05f)
        DrawLine(Project(center + a), Project(center + b), color, lineWidth, antialiased: true);
    }
  }

  /// <summary>
  /// Капюшон с накидкой: охватывает голову, заострён к затылку и спадает на плечи. Спереди — проём с отделкой,
  /// в глубине которого лицо в тени.
  /// </summary>
  private void DrawHood(Proportions body, Skeleton pose, float top)
  {
    var radius = body.Head;
    var center = Project(pose.Head);
    var points = new List<Vector2>();
    for (var i = 0; i < 18; i++)
      points.Add(center + Vector2.FromAngle(Mathf.Tau * i / 18) * radius * 1.2f);
    points.Add(Project(pose.Head + _forward * (-radius * 0.8f) + Vector3.Up * radius * 1.05f));
    foreach (var side in new[] { 1f, -1f })
    {
      points.Add(Project(pose.Shoulder + _right * (side * body.ChestWidth * 0.95f) + Vector3.Up * 0.3f));
      points.Add(Project(pose.Shoulder + _forward * (side * body.Chest * 1f) + Vector3.Up * 0.5f));
    }

    var hull = ConvexHull(points);
    DrawPolygon(hull, Lit(hull, hull.Select(point => (point - center).Normalized()).ToArray(), Color, top, 0.45f));
    DrawPolyline([.. hull, hull[0]], Color.Lightened(0.22f) with { A = 0.55f }, 0.5f, antialiased: true);

    if (_forward.Z > 0.35f)
      return;

    // Проём: анфас — во всю ширину, в профиль — узкая щель у переднего края.
    var squeeze = Mathf.Clamp(0.35f - _forward.Z * 0.65f, 0.15f, 1f);
    var middle = Project(pose.Head + _forward * radius * 0.6f + Vector3.Down * radius * 0.05f);
    var opening = Enumerable.Range(0, 20)
      .Select(i => Mathf.Tau * i / 20)
      .Select(angle =>
      {
        var x = Mathf.Cos(angle) * radius * 0.8f * squeeze;
        var y = Mathf.Sin(angle) * radius * 1f;
        // Верх проёма — стрельчатый.
        if (y < 0)
        {
          x *= 1 + 0.3f * Mathf.Sin(angle);
          y *= 1.1f;
        }

        return middle + new Vector2(x, y);
      })
      .ToArray();
    DrawColoredPolygon(opening, HoodOpeningColor);

    // Лицо в глубине: видна только нижняя, освещённая снизу часть.
    var face = Enumerable.Range(0, 16)
      .Select(i => Mathf.Tau * i / 16)
      .Select(angle => middle + new Vector2(0, radius * 0.25f) + new Vector2(Mathf.Cos(angle) * radius * 0.55f * squeeze, Mathf.Sin(angle) * radius * 0.6f))
      .ToArray();
    DrawColoredPolygon(face, SkinTone.Darkened(0.35f));
    if (_forward.Z < 0.05f)
      DrawFaceFeatures(pose.Head, radius * 0.92f, full: false);
    DrawPolyline([.. opening, opening[0]], RobeTrimColor, 0.45f, antialiased: true);
  }

  /// <summary>
  /// Выпуклая оболочка точек — замкнутый многоугольник без повтора первой точки в конце.
  /// </summary>
  private static Vector2[] ConvexHull(List<Vector2> points)
  {
    var hull = Geometry2D.ConvexHull(points.ToArray());
    return hull.Length > 1 && hull[0].IsEqualApprox(hull[^1]) ? hull[..^1] : hull;
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
