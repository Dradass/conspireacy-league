using ConspiracyLeague.Core.Theories;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Окно информации о карточке поверх стола. Закрывается по Esc, клику вне окна или кнопке «Закрыть».
/// Ссылки на теории открывают их описание; «Назад» возвращает к предыдущему тексту.
/// </summary>
public partial class CardInfoPanel : Control
{
  private const int TextWidth = 560;
  private const int TextFontSize = 20;
  private const string TheoryLinkPrefix = "theory:";

  private readonly List<string> _history = [];
  private ColorRect _dim = null!;
  private CenterContainer _center = null!;
  private RichTextLabel _text = null!;
  private Button _back = null!;

  public override void _Ready()
  {
    MouseFilter = MouseFilterEnum.Stop;
    Visible = false;

    _dim = new ColorRect { Color = new Color(0, 0, 0, 0.5f), MouseFilter = MouseFilterEnum.Ignore };
    AddChild(_dim);

    _center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
    AddChild(_center);

    var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
    _center.AddChild(panel);

    var column = new VBoxContainer();
    column.AddThemeConstantOverride("separation", 12);
    panel.AddChild(column);

    _text = new RichTextLabel
    {
      BbcodeEnabled = true,
      FitContent = true,
      CustomMinimumSize = new Vector2(TextWidth, 0),
    };
    _text.AddThemeFontSizeOverride("normal_font_size", TextFontSize);
    // Описание карточки — как досье: машинописный шрифт.
    _text.AddThemeFontOverride("normal_font", UiStyle.DossierFont);
    _text.MetaClicked += OnMetaClicked;
    column.AddChild(_text);

    var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
    buttons.AddThemeConstantOverride("separation", 12);
    column.AddChild(buttons);

    _back = new Button { Text = "Назад", FocusMode = FocusModeEnum.None };
    _back.Pressed += Back;
    buttons.AddChild(_back);

    var close = new Button { Text = "Закрыть", FocusMode = FocusModeEnum.None };
    close.Pressed += Close;
    buttons.AddChild(close);
  }

  public override void _GuiInput(InputEvent @event)
  {
    // Клик левой кнопкой по затемнённому фону (мимо окна) закрывает окно. Колесо и правая кнопка не закрывают.
    if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
    {
      AcceptEvent();
      Close();
    }
  }

  public override void _UnhandledInput(InputEvent @event)
  {
    if (!Visible || !@event.IsActionPressed("ui_cancel"))
      return;

    GetViewport().SetInputAsHandled();
    Close();
  }

  /// <summary>
  /// Показать текст (BBCode) и запомнить предыдущий, чтобы вернуться кнопкой «Назад».
  /// </summary>
  public void Open(string content)
  {
    _history.Add(content);
    ShowCurrent();
  }

  public override void _Process(double delta)
  {
    if (Visible)
      Layout();
  }

  /// <summary>
  /// Разместить затемнение и центрированное окно по размеру экрана. Якоря не используем: при открытии окно
  /// иначе могло появиться без размера.
  /// </summary>
  private void Layout()
  {
    var screen = GetViewportRect().Size;
    Position = Vector2.Zero;
    Size = screen;
    _dim.Position = Vector2.Zero;
    _dim.Size = screen;
    _center.Position = Vector2.Zero;
    _center.Size = screen;
  }

  private void ShowCurrent()
  {
    _text.Text = _history[^1];
    _back.Visible = _history.Count > 1;
    Layout();
    Visible = true;
  }

  private void Back()
  {
    if (_history.Count < 2)
      return;

    _history.RemoveAt(_history.Count - 1);
    ShowCurrent();
  }

  private void Close()
  {
    _history.Clear();
    Visible = false;
  }

  private void OnMetaClicked(Variant meta)
  {
    var link = meta.AsString();
    if (!link.StartsWith(TheoryLinkPrefix, StringComparison.Ordinal))
      return;

    if (int.TryParse(link.AsSpan(TheoryLinkPrefix.Length), out var index) && index >= 0 && index < TheoryCatalog.All.Count)
      Open(CardInfoText.TheoryText(TheoryCatalog.All[index]));
  }
}
