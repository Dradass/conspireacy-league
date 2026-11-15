using Godot;

namespace ConspiracyLeague.Game.Cards;

/// <summary>
/// Деревянная фигурка человечка (мипл) в 2.5D: передняя грань, видимая толщина и тень на столе.
/// Начало координат узла — точка, где фигурка стоит на столе (между ступнями).
/// </summary>
public partial class MeepleView : Node2D
{
  /// <summary>
  /// Облик фигурки (какой теории какой облик — см. <c>Board.StyleOf</c>).
  /// </summary>
  public enum MeepleLook
  {
    /// <summary>Обычный мипл цвета <see cref="Color"/>.</summary>
    Wooden,

    /// <summary>Инопланетянин, всегда зелёный.</summary>
    Alien,

    /// <summary>Человек в мантии цвета <see cref="Color"/>.</summary>
    Robed,

    /// <summary>Мипл в шляпке мухомора.</summary>
    Mushroom,

    /// <summary>Мипл в деловом костюме цвета <see cref="Color"/>: серая голова, белый воротник и галстук.</summary>
    Suit,

    /// <summary>Наполовину обычный серый мипл, наполовину — в деловом костюме цвета <see cref="Color"/>.</summary>
    HalfSuit,

    /// <summary>Мипл с рожками.</summary>
    Horned,

    /// <summary>Мипл в шапочке из фольги.</summary>
    FoilHat,

    /// <summary>Мипл в белом медицинском халате: отвороты, рубашка в вырезе, пуговицы и нагрудный карман с ручкой.</summary>
    LabCoat,
  }

  /// <summary>
  /// Обычный серый цвет фигурки.
  /// </summary>
  public static readonly Color Gray = new("9b9b9b");

  /// <summary>
  /// Размер фигурки (без толщины).
  /// </summary>
  public static readonly Vector2 Size = new(22, 26);

  private const float Depth = 5f;
  private const int ArcSegments = 14;

  /// <summary>
  /// Направление толщины: фигурку видно чуть сверху и справа.
  /// </summary>
  private static readonly Vector2 DepthDirection = new Vector2(0.6f, -0.8f).Normalized();

  private static readonly Color ShadowColor = new(0, 0, 0, 0.35f);
  private static readonly Color OutlineColor = new("4a2f14");
  private static readonly Color ShirtColor = new("f4f4f4");
  private static readonly Color TieColor = new("b3202a");
  private static readonly Color CapColor = new("d8231b");
  private static readonly Color CapDotColor = new("fbf6ea");
  private static readonly Color HornColor = new("140303");
  private static readonly Color HornEdgeColor = new("e8c8a8");
  private static readonly Color FoilColor = new("d9e2ea");
  private static readonly Color FoilEdgeColor = new("5b6b78");
  private static readonly Color CoatSeamColor = new("8c9aa6");
  private static readonly Color CoatShirtColor = new("8fc3e8");
  private static readonly Color PenColor = new("2a5bd7");
  private static readonly Color HighlightColor = new(1, 1, 1, 0.35f);

  /// <summary>
  /// Контур фигурки в долях размера: x от −0.5 до 0.5, y от 0 (макушка) до 1 (ступни).
  /// </summary>
  private static readonly Vector2[] Outline = CreateOutline();

  /// <summary>
  /// Голова фигурки в тех же долях, что и <see cref="Outline"/>.
  /// </summary>
  private static readonly Vector2[] Head = CreateHead();

  /// <summary>
  /// Правая половина туловища (без головы) в тех же долях, что и <see cref="Outline"/>.
  /// </summary>
  private static readonly Vector2[] RightBody = CreateRightBody();

  private float _height;

  /// <summary>
  /// Цвет фигурки.
  /// </summary>
  public Color Color { get; init; } = new("d9a55b");

  /// <summary>
  /// Облик фигурки. Инопланетянин всегда зелёный; у остальных <see cref="Color"/> — цвет тела, мантии или костюма.
  /// </summary>
  public MeepleLook Look { get; init; } = MeepleLook.Wooden;

  /// <summary>
  /// Высота над столом: фигурка поднимается, а тень остаётся на месте.
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

  public override void _Draw()
  {
    // Тень сжимается, когда фигурка поднята.
    var shadowScale = 1f / (1f + _height / Size.Y);
    DrawSetTransform(new Vector2(Depth * 0.3f, 0), 0, new Vector2(Size.X * 0.55f, Size.X * 0.18f) * shadowScale);
    DrawCircle(Vector2.Zero, 1, ShadowColor);
    DrawSetTransform(Vector2.Zero);

    switch (Look)
    {
      case MeepleLook.Alien:
        DrawAlien();
        return;
      case MeepleLook.Robed:
        DrawRobed();
        return;
    }

    var regions = Look switch
    {
      MeepleLook.Suit => new (Vector2[] Shape, Color Color)[] { (Outline, Color), (Head, Gray) },
      MeepleLook.HalfSuit => [(Outline, Gray), (RightBody, Color)],
      _ => [(Outline, Color)],
    };
    DrawFigure(regions);

    switch (Look)
    {
      case MeepleLook.Suit:
        DrawSuitFront(half: false);
        break;
      case MeepleLook.HalfSuit:
        DrawSuitFront(half: true);
        break;
      case MeepleLook.Mushroom:
        DrawMushroomCap();
        break;
      case MeepleLook.Horned:
        DrawHorns();
        break;
      case MeepleLook.FoilHat:
        DrawFoilHat();
        break;
      case MeepleLook.LabCoat:
        DrawLabCoat();
        break;
    }
  }

  /// <summary>
  /// Объёмная фигурка из нескольких областей разного цвета: сначала толщина всех областей, потом их передние грани,
  /// потом общий контур. Первая область — силуэт целиком.
  /// </summary>
  private void DrawFigure(IReadOnlyList<(Vector2[] Shape, Color Color)> regions)
  {
    var outline = regions[0].Color.Darkened(0.65f);

    // Толщина: слои от задней грани к передней, задние темнее.
    foreach (var (shape, color) in regions)
    {
      var front = shape.Select(ToLocal).ToArray();
      var side = color.Darkened(0.45f);
      for (var layer = Depth; layer > 0; layer -= 1f)
      {
        var offset = DepthDirection * layer;
        DrawColoredPolygon(front.Select(point => point + offset).ToArray(), side.Lerp(color.Darkened(0.25f), 1 - layer / Depth));
      }
    }

    var silhouette = Outline.Select(ToLocal).ToArray();
    var back = silhouette.Select(point => point + DepthDirection * Depth).ToArray();
    DrawPolyline([.. back, back[0]], outline with { A = 0.6f }, 1f, antialiased: true);

    // Передние грани: сверху светлее, снизу темнее.
    foreach (var (shape, color) in regions)
    {
      var top = color.Lightened(0.2f);
      var bottom = color.Darkened(0.12f);
      DrawPolygon(shape.Select(ToLocal).ToArray(), shape.Select(point => top.Lerp(bottom, point.Y)).ToArray());
    }

    DrawPolyline([.. silhouette, silhouette[0]], outline, 1.2f, antialiased: true);

    // Блик на голове.
    DrawCircle(new Vector2(-Size.X * 0.06f, -Size.Y * 0.88f - _height), Size.X * 0.06f, HighlightColor);
  }

  /// <summary>
  /// Белый воротник рубашки и галстук; у половинного костюма (<paramref name="half"/>) — только на правой половине.
  /// </summary>
  private void DrawSuitFront(bool half)
  {
    var left = half ? 0f : -0.11f;
    DrawColoredPolygon([At(left, 0.39f), At(0.11f, 0.39f), At(0, 0.53f)], ShirtColor);
    var tie = half
      ? new[] { At(0, 0.42f), At(0.03f, 0.42f), At(0.045f, 0.62f), At(0, 0.68f) }
      : [At(-0.03f, 0.42f), At(0.03f, 0.42f), At(0.045f, 0.62f), At(0, 0.68f), At(-0.045f, 0.62f)];
    DrawColoredPolygon(tie, TieColor);
  }

  /// <summary>
  /// Шляпка мухомора: красный купол с белыми точками над головой.
  /// </summary>
  private void DrawMushroomCap()
  {
    const int segments = 16;
    var cap = new List<Vector2>();
    for (var i = 0; i <= segments; i++)
    {
      var angle = Mathf.Pi + Mathf.Pi * i / segments;
      cap.Add(At(Mathf.Cos(angle) * 0.36f, 0.16f + Mathf.Sin(angle) * 0.2f));
    }

    // Нижний край шляпки чуть выгнут вниз.
    cap.Add(At(0.18f, 0.2f));
    cap.Add(At(-0.18f, 0.2f));
    var points = cap.ToArray();
    DrawColoredPolygon(points, CapColor);
    DrawPolyline([.. points, points[0]], CapColor.Darkened(0.5f), 1f, antialiased: true);

    foreach (var (x, y, r) in new[] { (-0.2f, 0.1f, 0.05f), (0.02f, 0.02f, 0.06f), (0.2f, 0.09f, 0.045f), (-0.06f, 0.13f, 0.035f) })
      DrawCircle(At(x, y), Size.X * r, CapDotColor);
  }

  /// <summary>
  /// Рожки на макушке.
  /// </summary>
  private void DrawHorns()
  {
    foreach (var side in new[] { -1f, 1f })
    {
      Vector2[] horn = [At(side * 0.04f, 0.07f), At(side * 0.17f, 0.13f), At(side * 0.25f, 0.02f), At(side * 0.27f, -0.14f), At(side * 0.15f, -0.01f)];
      DrawColoredPolygon(horn, HornColor);
      DrawPolyline([.. horn, horn[0]], HornEdgeColor, 0.6f, antialiased: true);
    }
  }

  /// <summary>
  /// Остроконечная шапочка из фольги с полями.
  /// </summary>
  private void DrawFoilHat()
  {
    Vector2[] cone = [At(-0.19f, 0.1f), At(0.02f, -0.3f), At(0.19f, 0.1f)];
    DrawColoredPolygon(cone, FoilColor);
    DrawPolyline([.. cone, cone[0]], FoilEdgeColor, 1f, antialiased: true);

    Vector2[] brim = [At(-0.27f, 0.08f), At(0.27f, 0.08f), At(0.27f, 0.14f), At(-0.27f, 0.14f)];
    DrawColoredPolygon(brim, FoilColor);
    DrawPolyline([.. brim, brim[0]], FoilEdgeColor, 1f, antialiased: true);

    // Складки фольги.
    DrawLine(At(-0.04f, 0.02f), At(0.04f, -0.1f), FoilEdgeColor, 0.8f, antialiased: true);
    DrawLine(At(0.03f, 0.07f), At(-0.03f, -0.04f), FoilEdgeColor, 0.8f, antialiased: true);
  }

  /// <summary>
  /// Медицинский халат: голубая рубашка в вырезе, отвороты, застёжка с пуговицами и нагрудный карман с ручкой.
  /// </summary>
  private void DrawLabCoat()
  {
    DrawColoredPolygon([At(-0.09f, 0.39f), At(0.09f, 0.39f), At(0, 0.56f)], CoatShirtColor);

    // Отвороты и застёжка до низа халата.
    DrawPolyline([At(-0.13f, 0.39f), At(-0.06f, 0.5f), At(0, 0.6f)], CoatSeamColor, 1f, antialiased: true);
    DrawPolyline([At(0.13f, 0.39f), At(0.06f, 0.5f), At(0, 0.6f)], CoatSeamColor, 1f, antialiased: true);
    DrawLine(At(0, 0.6f), At(0, 0.82f), CoatSeamColor, 0.8f, antialiased: true);
    foreach (var y in new[] { 0.66f, 0.74f })
      DrawCircle(At(0.035f, y), Size.X * 0.022f, CoatSeamColor);

    // Нагрудный карман с ручкой — слева на груди фигурки (справа на экране).
    Vector2[] pocket = [At(0.14f, 0.47f), At(0.27f, 0.47f), At(0.26f, 0.56f), At(0.15f, 0.56f)];
    DrawPolyline([.. pocket, pocket[0]], CoatSeamColor, 0.8f, antialiased: true);
    DrawLine(At(0.19f, 0.43f), At(0.19f, 0.5f), PenColor, 1.2f);
  }

  private Vector2 ToLocal(Vector2 point) => At(point.X, point.Y);

  /// <summary>
  /// Точка фигурки: x — в долях ширины от центра, y — в долях высоты от макушки (0) до ступней (1), с учётом высоты над столом.
  /// </summary>
  private Vector2 At(float x, float y) => new(x * Size.X, (y - 1) * Size.Y - _height);

  /// <summary>
  /// Инопланетянин: зелёный, тонкое туловище, большая голова с огромными чёрными глазами.
  /// </summary>
  private void DrawAlien()
  {
    var green = new Color("5fd35a");
    var outline = new Color("1d4d1b");

    var legs = new[] { At(-0.02f, 0.8f), At(-0.1f, 1f) };
    DrawLine(legs[0], legs[1], green, 2f, antialiased: true);
    DrawLine(At(0.02f, 0.8f), At(0.1f, 1f), green, 2f, antialiased: true);

    DrawLine(At(-0.05f, 0.46f), At(-0.22f, 0.66f), green, 2f, antialiased: true);
    DrawLine(At(0.05f, 0.46f), At(0.22f, 0.66f), green, 2f, antialiased: true);

    DrawColoredPolygon([At(-0.05f, 0.42f), At(0.05f, 0.42f), At(0.04f, 0.8f), At(-0.04f, 0.8f)], green);

    var head = At(0, 0.2f);
    var headRadius = Size.X * 0.28f;
    DrawCircle(head, headRadius, green);
    DrawArc(head, headRadius, 0, Mathf.Tau, 24, outline, 1.2f, antialiased: true);

    // Огромные чёрные глаза с бликом.
    var eyeRadius = Size.X * 0.13f;
    foreach (var side in new[] { -1f, 1f })
    {
      var eye = At(side * 0.12f, 0.18f);
      DrawCircle(eye, eyeRadius, Colors.Black);
      DrawCircle(eye + new Vector2(-eyeRadius * 0.3f, -eyeRadius * 0.3f), eyeRadius * 0.3f, HighlightColor);
    }
  }

  /// <summary>
  /// Человек в мантии: капюшон, лицо в тени и широкая мантия цвета <see cref="Color"/>.
  /// </summary>
  private void DrawRobed()
  {
    var skin = new Color("e8c9a0");
    var robeDark = Color.Darkened(0.35f);

    var robe = new[] { At(-0.1f, 0.36f), At(0.1f, 0.36f), At(0.26f, 0.62f), At(0.4f, 1f), At(-0.4f, 1f), At(-0.26f, 0.62f) };
    DrawColoredPolygon(robe, Color);
    DrawPolyline([.. robe, robe[0]], OutlineColor, 1f, antialiased: true);

    var hood = new[] { At(-0.16f, 0.3f), At(0, -0.05f), At(0.16f, 0.3f) };
    DrawColoredPolygon(hood, Color);
    DrawPolyline([.. hood, hood[0]], OutlineColor, 1f, antialiased: true);

    // Лицо глубоко в капюшоне.
    DrawCircle(At(0, 0.2f), Size.X * 0.14f, robeDark);
    DrawCircle(At(0, 0.22f), Size.X * 0.1f, skin);
  }

  private static Vector2[] CreateHead()
  {
    const int segments = 20;
    return Enumerable.Range(0, segments)
      .Select(i => Mathf.Tau * i / segments)
      .Select(angle => new Vector2(0, 0.2f) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.9f) * 0.18f)
      .ToArray();
  }

  private static Vector2[] CreateRightBody() =>
  [
    new(0, 0.37f),
    new(0.09f, 0.34f),
    new(0.14f, 0.39f),
    new(0.38f, 0.42f),
    new(0.47f, 0.45f),
    new(0.50f, 0.50f),
    new(0.46f, 0.55f),
    new(0.36f, 0.58f),
    new(0.22f, 0.60f),
    new(0.26f, 0.76f),
    new(0.40f, 0.95f),
    new(0.37f, 1.00f),
    new(0.13f, 1.00f),
    new(0.07f, 0.88f),
    new(0, 0.82f),
  ];

  private static Vector2[] CreateOutline()
  {
    var points = new List<Vector2>();

    // Голова: от левой стороны шеи через макушку к правой (углы — по часовой стрелке на экране, y вниз).
    var head = new Vector2(0, 0.2f);
    const float headRadius = 0.18f;
    var from = Mathf.DegToRad(120);
    var to = Mathf.DegToRad(420);
    for (var i = 0; i <= ArcSegments; i++)
    {
      var angle = Mathf.Lerp(from, to, (float)i / ArcSegments);
      points.Add(head + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.9f) * headRadius);
    }

    // Правая половина туловища сверху вниз до промежности.
    Vector2[] right =
    [
      new(0.14f, 0.39f),
      new(0.38f, 0.42f),
      new(0.47f, 0.45f),
      new(0.50f, 0.50f),
      new(0.46f, 0.55f),
      new(0.36f, 0.58f),
      new(0.22f, 0.60f),
      new(0.26f, 0.76f),
      new(0.40f, 0.95f),
      new(0.37f, 1.00f),
      new(0.13f, 1.00f),
      new(0.07f, 0.88f),
    ];
    points.AddRange(right);
    points.Add(new Vector2(0, 0.82f));
    points.AddRange(right.Reverse().Select(point => point with { X = -point.X }));
    return [.. points];
  }
}
