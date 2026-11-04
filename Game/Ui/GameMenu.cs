using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Меню в левом верхнем углу во время партии: кнопка с тремя полосками раскрывает «Выйти в главное меню» и «Выйти».
/// </summary>
public partial class GameMenu : VBoxContainer
{
  private const string StartMenuScene = "res://Game/StartMenu.tscn";
  private const float Margin = 24f;
  private const float IconSize = 48f;
  private const float BarHeight = 4f;
  private const float PanelWidth = 280f;
  private const int ButtonFontSize = 20;
  private const float ButtonHeight = 48f;

  private static readonly Color PanelColor = new(0.08f, 0.1f, 0.09f, 0.95f);
  private static readonly Color IconColor = new(0.9f, 0.9f, 0.9f);

  private PanelContainer _panel = null!;

  public override void _Ready()
  {
    SetAnchorsPreset(LayoutPreset.TopLeft);
    OffsetLeft = Margin;
    OffsetTop = Margin;
    MouseFilter = MouseFilterEnum.Ignore;
    AddThemeConstantOverride("separation", 8);

    var toggle = new Button
    {
      Name = "Toggle",
      CustomMinimumSize = new Vector2(IconSize, IconSize),
      FocusMode = FocusModeEnum.None,
    };
    var icon = new Control { MouseFilter = MouseFilterEnum.Ignore };
    icon.SetAnchorsPreset(LayoutPreset.FullRect);
    icon.Draw += () => DrawBars(icon);
    toggle.AddChild(icon);
    toggle.Pressed += () => _panel.Visible = !_panel.Visible;
    AddChild(toggle);

    var style = new StyleBoxFlat { BgColor = PanelColor };
    style.SetCornerRadiusAll(8);
    style.SetContentMarginAll(12);

    _panel = new PanelContainer { Name = "Panel", Visible = false, CustomMinimumSize = new Vector2(PanelWidth, 0) };
    _panel.AddThemeStyleboxOverride("panel", style);

    var column = new VBoxContainer();
    column.AddThemeConstantOverride("separation", 8);
    column.AddChild(CreateButton("Menu", "Выйти в главное меню", () => GetTree().ChangeSceneToFile(StartMenuScene)));
    column.AddChild(CreateButton("Quit", "Выйти", () => GetTree().Quit()));
    _panel.AddChild(column);
    AddChild(_panel);
  }

  /// <summary>
  /// Три горизонтальные полоски, равномерно расположенные по высоте значка.
  /// </summary>
  private static void DrawBars(Control icon)
  {
    var size = icon.Size;
    var width = size.X * 0.5f;
    var x = (size.X - width) / 2;
    for (var i = 0; i < 3; i++)
    {
      var y = size.Y * (0.3f + 0.2f * i) - BarHeight / 2;
      icon.DrawRect(new Rect2(x, y, width, BarHeight), IconColor);
    }
  }

  private static Button CreateButton(string name, string text, Action pressed)
  {
    var button = new Button
    {
      Name = name,
      Text = text,
      CustomMinimumSize = new Vector2(0, ButtonHeight),
      FocusMode = Control.FocusModeEnum.None,
    };
    button.AddThemeFontSizeOverride("font_size", ButtonFontSize);
    button.Pressed += pressed;
    return button;
  }
}
