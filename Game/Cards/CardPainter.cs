using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Cards.Professionals;
using Godot;

namespace ConspiracyLeague.Game.Cards;

/// <summary>
/// Отрисовка карточек: лицевая сторона и рубашка. Используется и картами на столе, и колодами.
/// </summary>
public static class CardPainter
{
  /// <summary>
  /// Размер карточки (ширина : высота = 3 : 4).
  /// </summary>
  public static readonly Vector2 CardSize = new(120, 160);

  /// <summary>
  /// Масштаб мипла владельца карточки.
  /// </summary>
  public const float OwnerMarkScale = 0.8f;

  private const float HeaderShare = 0.18f;
  private const float RibbonShare = 0.14f;
  private const float TheoryShare = 0.3f;

  private static readonly Color PaperColor = new("f3ecd9");
  private static readonly Color InkColor = new("fdfaf2");
  private static readonly Color TextColor = new("3b3328");
  private static readonly Color HeadquartersColor = new("c9962b");
  private static readonly Color ShadowColor = new(0, 0, 0, 0.45f);
  private static readonly Color PlainBackColor = new("000000");

  /// <summary>
  /// Цвет карточки по её типу.
  /// </summary>
  public static Color AccentOf(Card card) => card switch
  {
    HomelessCard => new Color("7a6a58"),
    CitizenCard => new Color("3f6e9e"),
    ProfessionalCard => new Color("8e4b8a"),
    TheoryCard => new Color("9e3f3f"),
    _ => new Color("555555"),
  };

  /// <summary>
  /// Где стоит мипл владельца карточки: справа от названия теории. Точка — ступни фигурки,
  /// относительно центра карточки.
  /// </summary>
  public static Vector2 OwnerMarkPosition(Rect2 rect)
  {
    var theoryRect = TheoryRect(rect, withOwnerMark: false);
    var size = OwnerMarkSize;
    return new Vector2(theoryRect.End.X - size.X / 2, theoryRect.GetCenter().Y + size.Y / 2) - rect.GetCenter();
  }

  private static Vector2 OwnerMarkSize => MeepleView.Size * OwnerMarkScale;

  /// <summary>
  /// Нарисовать лицевую сторону: вверху — финальный класс карточки, под ним — теория и влияние.
  /// </summary>
  /// <param name="canvas">Где рисовать.</param>
  /// <param name="rect">Прямоугольник карточки.</param>
  /// <param name="card">Карточка.</param>
  /// <param name="withShadow">Рисовать тень (карточку подняли со стола).</param>
  /// <param name="isHeadquarters">Карточка — штаб теории.</param>
  /// <param name="withOwnerMark">Справа от названия теории стоит мипл владельца — название сдвигается левее.</param>
  public static void DrawFace(
    CanvasItem canvas,
    Rect2 rect,
    Card card,
    bool withShadow = false,
    bool isHeadquarters = false,
    bool withOwnerMark = false)
  {
    var accent = AccentOf(card);
    var radius = CornerRadius(rect);

    var body = new StyleBoxFlat { BgColor = PaperColor, BorderColor = isHeadquarters ? HeadquartersColor : accent };
    body.SetBorderWidthAll(Mathf.Max(2, (int)(rect.Size.X * (isHeadquarters ? 0.05f : 0.025f))));
    body.SetCornerRadiusAll(radius);
    if (withShadow)
    {
      body.ShadowColor = ShadowColor;
      body.ShadowSize = 12;
      body.ShadowOffset = new Vector2(6, 8);
    }

    canvas.DrawStyleBox(body, rect);

    var headerRect = new Rect2(rect.Position, new Vector2(rect.Size.X, rect.Size.Y * HeaderShare));
    var header = new StyleBoxFlat
    {
      BgColor = accent,
      CornerRadiusTopLeft = radius,
      CornerRadiusTopRight = radius,
    };
    canvas.DrawStyleBox(header, headerRect);
    DrawCenteredText(canvas, card.Name, headerRect, (int)(headerRect.Size.Y * 0.5f), InkColor);

    var smallFont = (int)(rect.Size.Y * 0.075f);
    if (card.Theory is { } theory)
    {
      var theoryRect = TheoryRect(rect, withOwnerMark);
      DrawCenteredText(canvas, theory.Name, theoryRect, FitFontSize(theory.Name, theoryRect.Size.X, smallFont), TextColor);
    }

    var ribbonHeight = rect.Size.Y * RibbonShare;
    if (card is PersonCard person)
    {
      var bottom = rect.End.Y - (isHeadquarters ? ribbonHeight : 0);
      var influenceRect = new Rect2(rect.Position.X, bottom - ribbonHeight, rect.Size.X, ribbonHeight);
      DrawCenteredText(canvas, $"Влияние: {person.Influence}", influenceRect, smallFont, TextColor);
    }

    if (isHeadquarters)
    {
      var ribbonRect = new Rect2(rect.Position.X, rect.End.Y - ribbonHeight, rect.Size.X, ribbonHeight);
      var ribbon = new StyleBoxFlat
      {
        BgColor = HeadquartersColor,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
      };
      canvas.DrawStyleBox(ribbon, ribbonRect);
      DrawCenteredText(canvas, "Штаб теории", ribbonRect, smallFont, InkColor);
    }
  }

  /// <summary>
  /// Нарисовать рубашку карточки.
  /// </summary>
  /// <param name="canvas">Где рисовать.</param>
  /// <param name="rect">Прямоугольник карточки.</param>
  /// <param name="accent">Цвет колоды.</param>
  /// <param name="title">Подпись на рубашке или <c>null</c>.</param>
  public static void DrawBack(CanvasItem canvas, Rect2 rect, Color accent, string? title = null)
  {
    var radius = CornerRadius(rect);

    var back = new StyleBoxFlat { BgColor = accent, BorderColor = PaperColor };
    back.SetBorderWidthAll(2);
    back.SetCornerRadiusAll(radius);
    canvas.DrawStyleBox(back, rect);

    var innerRect = rect.Grow(-rect.Size.X * 0.08f);
    var inner = new StyleBoxFlat { DrawCenter = false, BorderColor = InkColor with { A = 0.5f } };
    inner.SetBorderWidthAll(1);
    inner.SetCornerRadiusAll(radius / 2);
    canvas.DrawStyleBox(inner, innerRect);

    if (title is not null)
      DrawCenteredText(canvas, title, innerRect.Grow(-rect.Size.X * 0.02f), (int)(rect.Size.Y * 0.075f), InkColor);
  }

  /// <summary>
  /// Нарисовать однотонную чёрную рубашку — так лежат использованные карточки.
  /// </summary>
  /// <param name="canvas">Где рисовать.</param>
  /// <param name="rect">Прямоугольник карточки.</param>
  public static void DrawPlainBack(CanvasItem canvas, Rect2 rect)
  {
    var back = new StyleBoxFlat { BgColor = PlainBackColor };
    back.SetCornerRadiusAll(CornerRadius(rect));
    canvas.DrawStyleBox(back, rect);
  }

  /// <summary>
  /// Нарисовать текст по центру области, перенося его по словам.
  /// </summary>
  public static void DrawCenteredText(CanvasItem canvas, string text, Rect2 area, int fontSize, Color color)
  {
    var font = ThemeDB.FallbackFont;
    var size = font.GetMultilineStringSize(text, HorizontalAlignment.Center, area.Size.X, fontSize);
    var top = area.Position.Y + (area.Size.Y - size.Y) / 2;
    canvas.DrawMultilineString(
      font,
      new Vector2(area.Position.X, top + font.GetAscent(fontSize)),
      text,
      HorizontalAlignment.Center,
      area.Size.X,
      fontSize,
      modulate: color);
  }

  /// <summary>
  /// Размер шрифта, при котором самое длинное слово текста помещается по ширине, но не больше заданного.
  /// </summary>
  private static int FitFontSize(string text, float width, int fontSize)
  {
    var font = ThemeDB.FallbackFont;
    var longest = text.Split(' ').MaxBy(word => font.GetStringSize(word, fontSize: fontSize).X)!;
    while (fontSize > 1 && font.GetStringSize(longest, fontSize: fontSize).X > width)
      fontSize--;
    return fontSize;
  }

  /// <summary>
  /// Область названия теории под заголовком. Если на карточке стоит мипл владельца, область заканчивается перед ним.
  /// </summary>
  private static Rect2 TheoryRect(Rect2 rect, bool withOwnerMark)
  {
    var padding = rect.Size.X * 0.06f;
    var top = rect.Position.Y + rect.Size.Y * HeaderShare;
    var width = rect.Size.X - padding * 2 - (withOwnerMark ? OwnerMarkSize.X + padding : 0);
    return new Rect2(rect.Position.X + padding, top, width, rect.Size.Y * TheoryShare);
  }

  private static int CornerRadius(Rect2 rect) => (int)(rect.Size.X * 0.07f);
}
