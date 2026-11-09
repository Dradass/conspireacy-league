using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Окно поверх стартового меню: затемнённый фон и панель по центру с заголовком. Esc закрывает окно
/// (см. <see cref="Cancel"/>).
/// </summary>
public partial class MenuDialog : Control
{
  protected const int TitleFontSize = 30;
  protected const int LabelFontSize = 22;
  protected const int ButtonFontSize = 24;
  protected const float ButtonHeight = 52f;
  private const float PanelWidth = 460f;

  private static readonly Color ShadeColor = new(0, 0, 0, 0.6f);

  /// <summary>
  /// Построить окно: затемнение, панель и заголовок.
  /// </summary>
  /// <returns>Столбец панели под заголовком — сюда добавляется содержимое окна.</returns>
  protected VBoxContainer BuildFrame(string title)
  {
    // Якоря и отступы сразу: одни якоря сохранили бы нулевой размер, с которым окно создано.
    SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    MouseFilter = MouseFilterEnum.Stop;
    AddChild(new ColorRect { Color = ShadeColor, AnchorRight = 1, AnchorBottom = 1 });

    var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
    AddChild(center);

    var panel = new PanelContainer { CustomMinimumSize = new Vector2(PanelWidth, 0) };
    panel.AddThemeStyleboxOverride("panel", UiStyle.Panel(margin: 22, alpha: 0.94f));
    center.AddChild(panel);

    var column = new VBoxContainer();
    column.AddThemeConstantOverride("separation", 14);
    panel.AddChild(column);

    var heading = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center };
    heading.AddThemeFontSizeOverride("font_size", TitleFontSize);
    UiStyle.MakeTitle(heading);
    column.AddChild(heading);
    return column;
  }

  /// <summary>
  /// Подпись в окне.
  /// </summary>
  protected static Label CreateLabel(string text, int fontSize = LabelFontSize)
  {
    var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
    label.AddThemeFontSizeOverride("font_size", fontSize);
    return label;
  }

  /// <summary>
  /// Кнопка окна, растянутая по ширине своего ряда.
  /// </summary>
  protected static Button CreateButton(string name, string text, Action pressed)
  {
    var button = new Button
    {
      Name = name,
      Text = text,
      CustomMinimumSize = new Vector2(0, ButtonHeight),
      SizeFlagsHorizontal = SizeFlags.ExpandFill,
      FocusMode = FocusModeEnum.None,
    };
    button.AddThemeFontSizeOverride("font_size", ButtonFontSize);
    button.Pressed += pressed;
    return button;
  }

  /// <summary>
  /// Окно закрыли кнопкой «Назад» или клавишей Esc.
  /// </summary>
  protected virtual void Cancel() => QueueFree();

  public override void _UnhandledInput(InputEvent @event)
  {
    if (!@event.IsActionPressed("ui_cancel"))
      return;

    GetViewport().SetInputAsHandled();
    Cancel();
  }
}
