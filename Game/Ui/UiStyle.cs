using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Оформление меню и интерфейса в стиле рубашки карточек: тёмные края, зелёное свечение, светло-зелёный текст.
/// Кнопки, списки, поля ввода, ползунки и панели получают его через тему по умолчанию (<see cref="Apply"/>),
/// панели со своим стилем — через <see cref="Panel"/>.
/// </summary>
public static class UiStyle
{
  /// <summary>
  /// Зелёное свечение — как центр рубашки.
  /// </summary>
  public static readonly Color Glow = new("5fd38a");

  /// <summary>
  /// Почти чёрный с зеленью — как края рубашки.
  /// </summary>
  public static readonly Color Dark = new("0b140e");

  /// <summary>
  /// Основной текст интерфейса.
  /// </summary>
  public static readonly Color Text = new("d7f5df");

  /// <summary>
  /// Заголовки.
  /// </summary>
  public static readonly Color Title = new("e8ffe9");

  /// <summary>
  /// Заголовки стартового меню — на зелёном фоне.
  /// </summary>
  public static readonly Color MenuTitle = Colors.Black;

  private static readonly Color PanelFill = new(0.05f, 0.1f, 0.07f, 0.9f);
  private static readonly Color ButtonFill = new(0.07f, 0.16f, 0.1f, 0.88f);
  private static readonly Color ButtonHoverFill = new(0.11f, 0.25f, 0.15f, 0.92f);
  private static readonly Color ButtonPressedFill = new(0.04f, 0.1f, 0.06f, 0.95f);
  private static readonly Color DisabledFill = new(0.06f, 0.09f, 0.07f, 0.7f);
  private static readonly Color TextHover = new("ffffff");
  private static readonly Color TextDisabled = new("6f8f78");

  private const int CornerRadius = 3;
  private const int PanelCornerRadius = 4;

  private static bool _applied;

  /// <summary>
  /// Основной шрифт интерфейса и карточек — Golos Text.
  /// </summary>
  public static Font TextFont { get; } = GD.Load<Font>("res://Game/Fonts/GolosText.ttf");

  /// <summary>
  /// Жирное начертание основного шрифта (для [b] в тексте).
  /// </summary>
  public static Font BoldFont { get; } = Weight(TextFont, 700);

  /// <summary>
  /// Шрифт заголовков и названий теорий — Cormorant Unicase.
  /// </summary>
  public static Font TitleFont { get; } = GD.Load<Font>("res://Game/Fonts/CormorantUnicase-SemiBold.ttf");

  /// <summary>
  /// Шрифт названий теорий на карточках — PT Serif Caption: засечки, но хорошо читается мелким кеглем.
  /// </summary>
  public static Font TheoryNameFont { get; } = GD.Load<Font>("res://Game/Fonts/PTSerifCaption.ttf");

  /// <summary>
  /// Шрифт чисел: деньги, проценты, стоимость — JetBrains Mono.
  /// </summary>
  public static Font NumberFont { get; } = Weight(GD.Load<Font>("res://Game/Fonts/JetBrainsMono.ttf"), 500);

  /// <summary>
  /// Шрифт «досье» — описания карточек и записи, — PT Mono.
  /// </summary>
  public static Font DossierFont { get; } = GD.Load<Font>("res://Game/Fonts/PTMono.ttf");

  /// <summary>
  /// Путь к шрифту заголовков для BBCode (<c>[font=…]</c>).
  /// </summary>
  public const string TitleFontPath = "res://Game/Fonts/CormorantUnicase-SemiBold.ttf";

  /// <summary>
  /// Панель: тёмная полупрозрачная, с зелёной рамкой и мягким зелёным свечением вокруг, как у рубашки.
  /// </summary>
  /// <param name="margin">Отступ содержимого от краёв.</param>
  /// <param name="alpha">Непрозрачность фона.</param>
  public static StyleBoxFlat Panel(int margin = 12, float alpha = 0.9f)
  {
    var style = new StyleBoxFlat
    {
      BgColor = PanelFill with { A = alpha },
      BorderColor = Glow with { A = 0.55f },
      ShadowColor = Glow with { A = 0.22f },
      ShadowSize = 8,
    };
    style.SetBorderWidthAll(1);
    style.SetCornerRadiusAll(PanelCornerRadius);
    style.SetContentMarginAll(margin);
    return style;
  }

  /// <summary>
  /// Перенести оформление в тему по умолчанию: все кнопки, списки, поля и панели без своего стиля выглядят одинаково.
  /// Вызывать до создания интерфейса; повторные вызовы ничего не делают.
  /// </summary>
  public static void Apply()
  {
    if (_applied)
      return;
    _applied = true;

    var theme = ThemeDB.GetDefaultTheme();
    theme.DefaultFont = TextFont;
    // Карточки и значки рисуют текст шрифтом по умолчанию движка.
    ThemeDB.FallbackFont = TextFont;
    theme.SetFont("normal_font", "RichTextLabel", TextFont);
    theme.SetFont("bold_font", "RichTextLabel", BoldFont);
    theme.SetFont("mono_font", "RichTextLabel", NumberFont);
    foreach (var type in new[] { "Button", "OptionButton", "MenuButton" })
    {
      theme.SetStylebox("normal", type, ButtonStyle(ButtonFill, Glow with { A = 0.35f }));
      theme.SetStylebox("hover", type, ButtonStyle(ButtonHoverFill, Glow, glow: true));
      theme.SetStylebox("pressed", type, ButtonStyle(ButtonPressedFill, Glow));
      theme.SetStylebox("hover_pressed", type, ButtonStyle(ButtonPressedFill, Glow, glow: true));
      theme.SetStylebox("disabled", type, ButtonStyle(DisabledFill, TextDisabled with { A = 0.4f }));
      theme.SetStylebox("focus", type, new StyleBoxEmpty());
      SetButtonFontColors(theme, type);
    }

    // У переключателей фона нет — только светлый текст.
    foreach (var type in new[] { "CheckButton", "CheckBox" })
      SetButtonFontColors(theme, type);

    theme.SetStylebox("normal", "LineEdit", ButtonStyle(ButtonPressedFill, Glow with { A = 0.45f }));
    theme.SetStylebox("focus", "LineEdit", ButtonStyle(ButtonPressedFill with { A = 0f }, Glow, glow: true));
    theme.SetStylebox("read_only", "LineEdit", ButtonStyle(DisabledFill, TextDisabled with { A = 0.4f }));
    theme.SetColor("font_color", "LineEdit", Text);
    theme.SetColor("caret_color", "LineEdit", Glow);
    theme.SetColor("selection_color", "LineEdit", Glow with { A = 0.35f });

    theme.SetStylebox("panel", "PanelContainer", Panel());
    theme.SetStylebox("panel", "PopupMenu", Panel(6, 0.96f));
    theme.SetStylebox("hover", "PopupMenu", ButtonStyle(ButtonHoverFill, Glow with { A = 0f }));
    theme.SetColor("font_color", "PopupMenu", Text);
    theme.SetColor("font_hover_color", "PopupMenu", TextHover);
    theme.SetColor("font_separator_color", "PopupMenu", Glow);
    theme.SetStylebox("panel", "TooltipPanel", Panel(8, 0.95f));
    theme.SetColor("font_color", "TooltipLabel", Text);

    var track = new StyleBoxFlat { BgColor = Dark with { A = 0.85f }, ContentMarginTop = 3, ContentMarginBottom = 3 };
    track.SetCornerRadiusAll(1);
    var filled = new StyleBoxFlat { BgColor = Glow, ContentMarginTop = 3, ContentMarginBottom = 3 };
    filled.SetCornerRadiusAll(1);
    theme.SetStylebox("slider", "HSlider", track);
    theme.SetStylebox("grabber_area", "HSlider", filled);
    theme.SetStylebox("grabber_area_highlight", "HSlider", filled);
  }

  /// <summary>
  /// Заголовок: шрифт Cormorant Unicase, один цвет — без контура и свечения.
  /// </summary>
  /// <param name="color">Цвет надписи; по умолчанию — цвет заголовков на тёмных панелях (<see cref="Title"/>).</param>
  public static void MakeTitle(Control control, Color? color = null)
  {
    control.AddThemeFontOverride("font", TitleFont);
    control.AddThemeColorOverride("font_color", color ?? Title);
  }

  /// <summary>
  /// Числовая надпись (деньги, проценты): шрифт JetBrains Mono.
  /// </summary>
  public static void MakeNumber(Control control) => control.AddThemeFontOverride("font", NumberFont);

  private static Font Weight(Font font, int weight) => new FontVariation
  {
    BaseFont = font,
    VariationOpentype = new Godot.Collections.Dictionary { { TextServerManager.GetPrimaryInterface().NameToTag("weight"), weight } },
  };

  private static void SetButtonFontColors(Theme theme, string type)
  {
    theme.SetColor("font_color", type, Text);
    theme.SetColor("font_hover_color", type, TextHover);
    theme.SetColor("font_pressed_color", type, Glow);
    theme.SetColor("font_hover_pressed_color", type, TextHover);
    theme.SetColor("font_focus_color", type, Text);
    theme.SetColor("font_disabled_color", type, TextDisabled);
  }

  private static StyleBoxFlat ButtonStyle(Color fill, Color border, bool glow = false)
  {
    var style = new StyleBoxFlat { BgColor = fill, BorderColor = border };
    style.SetBorderWidthAll(1);
    style.SetCornerRadiusAll(CornerRadius);
    style.ContentMarginLeft = 14;
    style.ContentMarginRight = 14;
    style.ContentMarginTop = 6;
    style.ContentMarginBottom = 6;
    if (glow)
    {
      style.ShadowColor = Glow with { A = 0.35f };
      style.ShadowSize = 6;
    }

    return style;
  }
}
