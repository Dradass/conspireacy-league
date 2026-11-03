using Godot;

namespace ConspiracyLeague.Game.Cards;

/// <summary>
/// Деревянная фигурка человечка (мипл) в 2.5D: передняя грань, видимая толщина и тень на столе.
/// Начало координат узла — точка, где фигурка стоит на столе (между ступнями).
/// </summary>
public partial class MeepleView : Node2D
{
  /// <summary>
  /// Облик фигурки: деревянный мипл, инопланетянин (теория «Инопланетяне среди нас») или человек в мантии (теория «Тайное правительство»).
  /// </summary>
  public enum MeepleLook
  {
    Wooden,
    Alien,
    Robed,
  }

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
  private static readonly Color HighlightColor = new(1, 1, 1, 0.35f);

  /// <summary>
  /// Контур фигурки в долях размера: x от −0.5 до 0.5, y от 0 (макушка) до 1 (ступни).
  /// </summary>
  private static readonly Vector2[] Outline = CreateOutline();

  private float _height;

  /// <summary>
  /// Цвет фигурки.
  /// </summary>
  public Color Color { get; init; } = new("d9a55b");

  /// <summary>
  /// Облик фигурки. Инопланетянин всегда зелёный, человек в мантии — цвета <see cref="Color"/> (мантия).
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

    var front =Outline.Select(point => new Vector2(point.X * Size.X, (point.Y - 1) * Size.Y - _height)).ToArray();

    // Толщина: слои от задней грани к передней, задние темнее.
    var side = Color.Darkened(0.45f);
    for (var layer = Depth; layer > 0; layer -= 1f)
    {
      var offset = DepthDirection * layer;
      DrawColoredPolygon(front.Select(point => point + offset).ToArray(), side.Lerp(Color.Darkened(0.25f), 1 - layer / Depth));
    }

    var back = front.Select(point => point + DepthDirection * Depth).ToArray();
    DrawPolyline([.. back, back[0]], OutlineColor with { A = 0.6f }, 1f, antialiased: true);

    // Передняя грань: сверху светлее, снизу темнее.
    var top = Color.Lightened(0.2f);
    var bottom = Color.Darkened(0.12f);
    var colors = Outline.Select(point => top.Lerp(bottom, point.Y)).ToArray();
    DrawPolygon(front, colors);
    DrawPolyline([.. front, front[0]], OutlineColor, 1.2f, antialiased: true);

    // Блик на голове.
    DrawCircle(new Vector2(-Size.X * 0.06f, -Size.Y * 0.88f - _height), Size.X * 0.06f, HighlightColor);
  }

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
