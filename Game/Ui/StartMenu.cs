using ConspiracyLeague.Core.Field;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Стартовое меню: выбор количества игроков, начало партии и выход из игры.
/// </summary>
public partial class StartMenu : Control
{
  /// <summary>
  /// Наименьшее количество игроков в партии.
  /// </summary>
  public const int MinPlayers = 2;

  private const int DefaultPlayers = 4;
  private const float MenuWidth = 420f;
  private const int TitleFontSize = 48;
  private const int LabelFontSize = 24;
  private const int ButtonFontSize = 28;
  private const float ButtonHeight = 60f;

  private static readonly Color BackgroundColor = new("26342c");
  private static readonly Color TitleColor = new("e0b84f");

  private Label _playersLabel = null!;
  private HSlider _players = null!;

  public override void _Ready()
  {
    SetAnchorsPreset(LayoutPreset.FullRect);
    AddChild(new ColorRect { Color = BackgroundColor, AnchorRight = 1, AnchorBottom = 1 });

    var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
    AddChild(center);

    var column = new VBoxContainer { CustomMinimumSize = new Vector2(MenuWidth, 0) };
    column.AddThemeConstantOverride("separation", 18);
    center.AddChild(column);

    var title = new Label { Text = "Conspiracy League", HorizontalAlignment = HorizontalAlignment.Center };
    title.AddThemeFontSizeOverride("font_size", TitleFontSize);
    title.AddThemeColorOverride("font_color", TitleColor);
    column.AddChild(title);
    column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 24) });

    _playersLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
    _playersLabel.AddThemeFontSizeOverride("font_size", LabelFontSize);
    column.AddChild(_playersLabel);

    _players = new HSlider
    {
      Name = "Players",
      MinValue = MinPlayers,
      MaxValue = FieldLayout.MaxPlayers,
      Step = 1,
      Value = DefaultPlayers,
      TickCount = FieldLayout.MaxPlayers - MinPlayers + 1,
      TicksOnBorders = true,
      FocusMode = FocusModeEnum.None,
    };
    _players.ValueChanged += _ => UpdatePlayersLabel();
    column.AddChild(_players);
    UpdatePlayersLabel();

    column.AddChild(CreateButton("Start", "Начать", StartGame));
    column.AddChild(CreateButton("Quit", "Выйти", () => GetTree().Quit()));
  }

  private int PlayerCount => (int)_players.Value;

  private void UpdatePlayersLabel() => _playersLabel.Text = $"Количество игроков: {PlayerCount}";

  private static Button CreateButton(string name, string text, Action pressed)
  {
    var button = new Button
    {
      Name = name,
      Text = text,
      CustomMinimumSize = new Vector2(0, ButtonHeight),
      FocusMode = FocusModeEnum.None,
    };
    button.AddThemeFontSizeOverride("font_size", ButtonFontSize);
    button.Pressed += pressed;
    return button;
  }

  /// <summary>
  /// Заменить меню партией с выбранным количеством игроков.
  /// </summary>
  private void StartGame()
  {
    var tree = GetTree();
    var game = new Main { Name = "Main", PlayerCount = PlayerCount };
    tree.Root.AddChild(game);
    tree.CurrentScene = game;
    QueueFree();
  }
}
