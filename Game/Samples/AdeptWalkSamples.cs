using ConspiracyLeague.Game.Cards;
using Godot;

namespace ConspiracyLeague.Game.Samples;

/// <summary>
/// Витрина шагающих адептов: облики во всех восьми направлениях крупно (шаг на месте)
/// и внизу — в игровом размере, гуляющие по полю в разные стороны.
/// </summary>
public partial class AdeptWalkSamples : Node2D
{
  /// <summary>
  /// Размер витрины; при записи кадров (см. <see cref="SampleCapture"/>) её увеличивают целиком.
  /// </summary>
  public static readonly Vector2 StageSize = new(1152, 864);

  private const float BigScale = 2.4f;
  private const float SmallScale = 1.6f;
  private const float LabelWidth = 110;
  private const float RowStep = 88;
  private const float FirstRow = 130;

  private static readonly Color TableColor = new("26342c");
  private static readonly Color TextColor = new("e9e4d8");

  private static readonly Rect2 Field = new(40, 650, StageSize.X - 80, 200);

  private static readonly (MeepleView.MeepleLook Look, Color Color, string Name)[] Looks =
  [
    (MeepleView.MeepleLook.Wooden, MeepleView.Gray, "Обычный"),
    (MeepleView.MeepleLook.Suit, new Color("2c3038"), "Костюм"),
    (MeepleView.MeepleLook.LabCoat, new Color("f2f2f2"), "Халат"),
    (MeepleView.MeepleLook.Robed, new Color("d9a55b"), "Мантия"),
    (MeepleView.MeepleLook.Horned, new Color("8a0a0a"), "Рожки"),
    (MeepleView.MeepleLook.Alien, MeepleView.Gray, "Пришелец"),
  ];

  private static readonly Vector2[] Directions =
  [
    new(0, -1), new(1, -1), new(1, 0), new(1, 1), new(0, 1), new(-1, 1), new(-1, 0), new(-1, -1),
  ];

  private readonly List<(WalkingAdeptView View, Vector2 Direction)> _strollers = [];

  public override void _Ready()
  {
    var step = (StageSize.X - LabelWidth - 20) / 8;
    for (var row = 0; row < Looks.Length; row++)
    {
      var (look, color, _) = Looks[row];
      for (var column = 0; column < 8; column++)
      {
        AddChild(new WalkingAdeptView
        {
          Build = WalkingAdeptView.BodyBuild.Slim, Look = look, Color = color, Walking = true, Phase = row * 0.9f,
          Heading = column * 45, Position = new Vector2(LabelWidth + step * (column + 0.5f), FirstRow + row * RowStep),
          Scale = Vector2.One * BigScale,
        });
      }
    }

    var random = new RandomNumberGenerator { Seed = 11 };
    var all = Enum.GetValues<MeepleView.MeepleLook>();
    var colors = new[] { MeepleView.Gray, new Color("e6b422"), new Color("1f4fb0"), new Color("ff1a1a"), new Color("2c3038") };
    for (var i = 0; i < 18; i++)
    {
      var direction = Directions[random.RandiRange(0, 7)];
      var view = new WalkingAdeptView
      {
        Build = WalkingAdeptView.BodyBuild.Slim, Look = all[i % all.Length], Color = colors[i % colors.Length],
        Walking = true, Phase = random.Randf() * Mathf.Tau, StepsPerSecond = random.RandfRange(2.2f, 3f),
        Position = new Vector2(random.RandfRange(Field.Position.X, Field.End.X), random.RandfRange(Field.Position.Y + 30, Field.End.Y)),
        Scale = Vector2.One * SmallScale,
      };
      view.FaceTowards(Motion(direction));
      _strollers.Add((view, direction));
      AddChild(view);
    }
  }

  /// <summary>
  /// Движение по экрану в одном из восьми направлений: по глубине (вверх-вниз) экранный шаг короче.
  /// </summary>
  private static Vector2 Motion(Vector2 direction) => new Vector2(direction.X, direction.Y * 0.55f).Normalized();

  public override void _Process(double delta)
  {
    for (var i = 0; i < _strollers.Count; i++)
    {
      var (view, direction) = _strollers[i];
      var next = view.Position + Motion(direction) * view.WalkSpeed * SmallScale * (float)delta;
      if (next.X < Field.Position.X || next.X > Field.End.X)
        direction.X = -direction.X;
      if (next.Y < Field.Position.Y + 30 || next.Y > Field.End.Y)
        direction.Y = -direction.Y;
      if (direction != _strollers[i].Direction)
      {
        view.FaceTowards(Motion(direction));
        _strollers[i] = (view, direction);
        continue;
      }

      view.Position = next;
    }

    // Задние (выше на экране) рисуются раньше передних.
    foreach (var (view, _) in _strollers)
      view.ZIndex = (int)view.Position.Y;
  }

  public override void _Draw()
  {
    DrawRect(new Rect2(Vector2.Zero, StageSize), TableColor);
    var font = ThemeDB.FallbackFont;
    DrawString(font, new Vector2(20, 34), "Образец B в восьми направлениях", fontSize: 20, modulate: TextColor);

    var step = (StageSize.X - LabelWidth - 20) / 8;
    for (var column = 0; column < 8; column++)
      DrawString(font, new Vector2(LabelWidth + step * column, 64), $"{column * 45}°", HorizontalAlignment.Center, step, 15, TextColor with { A = 0.75f });

    for (var row = 0; row < Looks.Length; row++)
      DrawString(font, new Vector2(20, FirstRow + row * RowStep - 20), Looks[row].Name, fontSize: 14, modulate: TextColor with { A = 0.75f });

    DrawString(font, new Vector2(20, Field.Position.Y), "В игровом размере (×1.6), гуляют в разные стороны", fontSize: 16, modulate: TextColor with { A = 0.7f });
  }
}
