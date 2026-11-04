using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Окно, в котором игрок вводит своё имя. Поле уже содержит имя по умолчанию («Игрок N»);
/// щелчок по нему стирает это значение.
/// </summary>
public partial class NameDialog : Control
{
  private const int TitleFontSize = 26;
  private const int FieldFontSize = 24;
  private const int ButtonFontSize = 22;
  private const float PanelWidth = 420f;

  private static readonly Color ShadeColor = new(0, 0, 0, 0.6f);
  private static readonly Color PanelColor = new("26342c");
  private static readonly Color TitleColor = new("e0b84f");

  private LineEdit _field = null!;
  private string _default = string.Empty;
  private TaskCompletionSource<string> _result = null!;

  /// <summary>
  /// Показать окно для игрока с номером <paramref name="number"/> поверх <paramref name="parent"/> и дождаться имени.
  /// </summary>
  /// <returns>Введённое имя; пустое поле даёт имя по умолчанию.</returns>
  public static Task<string> Ask(Node parent, int number)
  {
    var dialog = new NameDialog { Name = $"NameDialog{number}" };
    parent.AddChild(dialog);
    return dialog.Run(number);
  }

  private Task<string> Run(int number)
  {
    _default = $"Игрок {number}";
    _result = new TaskCompletionSource<string>();

    SetAnchorsPreset(LayoutPreset.FullRect);
    MouseFilter = MouseFilterEnum.Stop;
    AddChild(new ColorRect { Color = ShadeColor, AnchorRight = 1, AnchorBottom = 1 });

    var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
    AddChild(center);

    var panel = new PanelContainer { CustomMinimumSize = new Vector2(PanelWidth, 0) };
    var style = new StyleBoxFlat { BgColor = PanelColor, ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 20, ContentMarginBottom = 20 };
    panel.AddThemeStyleboxOverride("panel", style);
    center.AddChild(panel);

    var column = new VBoxContainer();
    column.AddThemeConstantOverride("separation", 16);
    panel.AddChild(column);

    var title = new Label { Text = $"Игрок {number}, введите имя", HorizontalAlignment = HorizontalAlignment.Center };
    title.AddThemeFontSizeOverride("font_size", TitleFontSize);
    title.AddThemeColorOverride("font_color", TitleColor);
    column.AddChild(title);

    _field = new NameField { Text = _default, Alignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(0, 44), DefaultName = _default };
    _field.AddThemeFontSizeOverride("font_size", FieldFontSize);
    _field.TextSubmitted += _ => Confirm();
    column.AddChild(_field);

    var ok = new Button { Text = "Готово", CustomMinimumSize = new Vector2(0, 48), FocusMode = FocusModeEnum.None };
    ok.AddThemeFontSizeOverride("font_size", ButtonFontSize);
    ok.Pressed += Confirm;
    column.AddChild(ok);

    _field.CallDeferred(Control.MethodName.GrabFocus);
    return _result.Task;
  }

  private void Confirm()
  {
    var name = _field.Text.Trim();
    _result.TrySetResult(name.Length == 0 ? _default : name);
    QueueFree();
  }

  /// <summary>
  /// Поле имени: по щелчку на имени по умолчанию оно стирается, чтобы игрок сразу начал печатать.
  /// </summary>
  private partial class NameField : LineEdit
  {
    /// <summary>
    /// Имя по умолчанию, которое стирается при щелчке.
    /// </summary>
    public string DefaultName { get; init; } = string.Empty;

    public override void _GuiInput(InputEvent @event)
    {
      if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && Text == DefaultName)
        Text = string.Empty;
      base._GuiInput(@event);
    }
  }
}
