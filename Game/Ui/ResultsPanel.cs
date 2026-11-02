using ConspiracyLeague.Core.Game;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Итоги партии: игроки по убыванию денег, победитель сверху, кнопки «В меню» и «Выйти».
/// Занимает весь экран и затемняет стол.
/// </summary>
public partial class ResultsPanel : Control
{
  private const string StartMenuScene = "res://Game/StartMenu.tscn";
  private const float PanelWidth = 460f;
  private const int TitleFontSize = 40;
  private const int RowFontSize = 28;
  private const int ButtonFontSize = 24;
  private const float ButtonHeight = 52f;

  private static readonly Color ShadeColor = new(0f, 0f, 0f, 0.6f);
  private static readonly Color PanelColor = new(0.08f, 0.1f, 0.09f, 0.95f);
  private static readonly Color TitleColor = new("e0b84f");

  /// <summary>
  /// Сессия игры: итоговый рейтинг игроков.
  /// </summary>
  public GameSession Session { get; init; } = null!;

  public override void _Ready()
  {
    SetAnchorsPreset(LayoutPreset.FullRect);
    AddChild(new ColorRect { Color = ShadeColor, AnchorRight = 1, AnchorBottom = 1 });

    var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
    AddChild(center);

    var style = new StyleBoxFlat { BgColor = PanelColor };
    style.SetCornerRadiusAll(12);
    style.SetContentMarginAll(28);
    var panel = new PanelContainer { CustomMinimumSize = new Vector2(PanelWidth, 0) };
    panel.AddThemeStyleboxOverride("panel", style);
    center.AddChild(panel);

    var column = new VBoxContainer();
    column.AddThemeConstantOverride("separation", 14);
    panel.AddChild(column);

    var ranking = Session.Ranking;
    var title = new Label { Text = $"Победил {ranking[0].Name}!", HorizontalAlignment = HorizontalAlignment.Center };
    title.AddThemeFontSizeOverride("font_size", TitleFontSize);
    title.AddThemeColorOverride("font_color", TitleColor);
    column.AddChild(title);

    for (var i = 0; i < ranking.Count; i++)
    {
      var player = ranking[i];
      var row = new Label { Text = $"{i + 1}. {player.Name} — {player.Money}$" };
      row.AddThemeFontSizeOverride("font_size", RowFontSize);
      row.AddThemeColorOverride("font_color", PlayerPalette.MainOf(player.Color));
      column.AddChild(row);
    }

    column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
    column.AddChild(CreateButton("Menu", "В меню", () => GetTree().ChangeSceneToFile(StartMenuScene)));
    column.AddChild(CreateButton("Quit", "Выйти", () => GetTree().Quit()));
  }

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
}
