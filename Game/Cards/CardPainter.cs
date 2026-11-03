using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Cards.Professionals;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;
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
  private const float ParentTheoryFontShare = 0.8f;
  private const int MinEffectFontSize = 6;

  private static readonly Color PaperColor = new("f3ecd9");
  private static readonly Color InkColor = new("fdfaf2");
  private static readonly Color TextColor = new("3b3328");
  private static readonly Color ParentTheoryColor = TextColor with { A = 0.55f };
  private static readonly Color HeadquartersColor = new("c9962b");
  private static readonly Color ShadowColor = new(0, 0, 0, 0.45f);
  private static readonly Color PlainBackColor = new("000000");
  private static readonly Color HiddenBackColor = new("2e3a4f");
  private static readonly Color ClosedOverlayColor = new(0.3f, 0.3f, 0.3f, 0.6f);
  private static readonly Color PeekOverlayColor = new(0f, 0f, 0f, 0.3f);
  private static readonly Color HazardYellow = new("f2c230");
  private static readonly Color HazardBlack = new("1a1a1a");
  private static readonly Color PenaltyColor = new("d1423a");
  private static readonly Color BonusColor = new("3fa34d");
  private static readonly Color ShieldColor = new("2f6fd1");
  private static readonly Color ShieldEdgeColor = new("fdfaf2");

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
    var headerFont = (int)(headerRect.Size.Y * 0.5f);
    if (card is PersonCard { Cost: var cost })
    {
      // Стоимость — справа в заголовке, название — по центру оставшегося места.
      var costText = $"{cost}$";
      var padding = rect.Size.X * 0.05f;
      var costWidth = ThemeDB.FallbackFont.GetStringSize(costText, fontSize: headerFont).X;
      var costRect = new Rect2(headerRect.End.X - padding - costWidth, headerRect.Position.Y, costWidth, headerRect.Size.Y);
      DrawCenteredText(canvas, costText, costRect, headerFont, InkColor);
      var nameRect = new Rect2(headerRect.Position.X + padding, headerRect.Position.Y, costRect.Position.X - headerRect.Position.X - padding * 2, headerRect.Size.Y);
      DrawCenteredText(canvas, card.Name, nameRect, FitFontSize(card.Name, nameRect.Size.X, headerFont), InkColor);
    }
    else
      DrawCenteredText(canvas, card.Name, headerRect, headerFont, InkColor);

    var smallFont = (int)(rect.Size.Y * 0.075f);
    if (card.Theory is { } theory)
      DrawTheoryName(canvas, TheoryRect(rect, withOwnerMark), theory, smallFont, withName: card is not TheoryCard);

    var ribbonHeight = rect.Size.Y * RibbonShare;
    if (card is TheoryCard theoryCard)
      DrawTheoryEffects(canvas, rect, theoryCard, smallFont);
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
  /// Нарисовать название теории, а у дочерней теории под ним — название родительской в скобках,
  /// мельче и бледнее. Обе надписи вместе стоят по центру области.
  /// Если название уже написано в шапке карточки (<paramref name="withName"/> = false), рисуется только родительская.
  /// </summary>
  private static void DrawTheoryName(CanvasItem canvas, Rect2 area, Theory theory, int fontSize, bool withName)
  {
    var nameFontSize = FitFontSize(theory.Name, area.Size.X, fontSize);
    if (theory.Parent is not { } parent)
    {
      if (withName)
        DrawCenteredText(canvas, theory.Name, area, nameFontSize, TextColor);
      return;
    }

    var parentText = $"({parent.Name})";
    var parentFontSize = FitFontSize(parentText, area.Size.X, (int)(fontSize * ParentTheoryFontShare));
    if (!withName)
    {
      DrawCenteredText(canvas, parentText, area, parentFontSize, ParentTheoryColor);
      return;
    }

    var nameHeight = TextHeight(theory.Name, area.Size.X, nameFontSize);
    var parentHeight = TextHeight(parentText, area.Size.X, parentFontSize);
    var top = area.Position.Y + (area.Size.Y - nameHeight - parentHeight) / 2;
    DrawCenteredText(canvas, theory.Name, new Rect2(area.Position.X, top, area.Size.X, nameHeight), nameFontSize, TextColor);
    DrawCenteredText(
      canvas,
      parentText,
      new Rect2(area.Position.X, top + nameHeight, area.Size.X, parentHeight),
      parentFontSize,
      ParentTheoryColor);
  }

  /// <summary>
  /// Нарисовать на карточке теории её эффект: действие при применении и пассивную способность.
  /// Текст под названием теории уменьшается, пока не поместится.
  /// </summary>
  private static void DrawTheoryEffects(CanvasItem canvas, Rect2 rect, TheoryCard card, int fontSize)
  {
    var lines = new List<string>();
    if (card.OnPlayFromHand != Effect.None)
      lines.Add(card.OnPlayFromHand.Description);
    if (card.Passive != PassiveAbility.None)
      lines.Add($"Пассивно: {card.Passive.Description}");
    var text = string.Join("\n", lines);

    var padding = rect.Size.X * 0.06f;
    var top = rect.Position.Y + rect.Size.Y * (HeaderShare + TheoryShare);
    var area = new Rect2(
      rect.Position.X + padding,
      top,
      rect.Size.X - padding * 2,
      rect.End.Y - padding - top);

    var size = fontSize;
    while (size > MinEffectFontSize && TextHeight(text, area.Size.X, size) > area.Size.Y)
      size--;
    DrawCenteredText(canvas, text, area, size, TextColor);
  }

  private static float TextHeight(string text, float width, int fontSize) =>
    ThemeDB.FallbackFont.GetMultilineStringSize(text, HorizontalAlignment.Center, width, fontSize).Y;

  /// <summary>
  /// Нарисовать щит в левом верхнем углу карточки: её защищает полицейский.
  /// </summary>
  /// <param name="canvas">Где рисовать.</param>
  /// <param name="rect">Прямоугольник карточки.</param>
  public static void DrawShield(CanvasItem canvas, Rect2 rect)
  {
    var width = rect.Size.X * 0.2f;
    var height = width * 1.2f;
    // Щит выступает за угол карточки, чтобы не закрывать название.
    var topLeft = rect.Position - new Vector2(width, height) * 0.3f;
    Vector2[] outline =
    [
      topLeft,
      topLeft + new Vector2(width / 2, -height * 0.1f),
      topLeft + new Vector2(width, 0),
      topLeft + new Vector2(width, height * 0.45f),
      topLeft + new Vector2(width / 2, height),
      topLeft + new Vector2(0, height * 0.45f),
    ];
    canvas.DrawColoredPolygon(outline, ShieldColor);
    canvas.DrawPolyline([.. outline, outline[0]], ShieldEdgeColor, 2, antialiased: true);
    var center = topLeft + new Vector2(width / 2, height * 0.42f);
    canvas.DrawLine(center + new Vector2(0, -height * 0.25f), center + new Vector2(0, height * 0.3f), ShieldEdgeColor, 2);
    canvas.DrawLine(center + new Vector2(-width * 0.25f, 0), center + new Vector2(width * 0.25f, 0), ShieldEdgeColor, 2);
  }

  /// <summary>
  /// Нарисовать изменение вероятности успеха персонажа: зелёная стрелка вверх и, например, «+10%»
  /// или красная стрелка вниз и «-10%».
  /// </summary>
  /// <param name="canvas">Где рисовать.</param>
  /// <param name="rect">Прямоугольник карточки.</param>
  /// <param name="modifier">На сколько процентов изменена вероятность успеха; не 0.</param>
  public static void DrawSuccessModifier(CanvasItem canvas, Rect2 rect, int modifier)
  {
    var up = modifier > 0;
    var color = up ? BonusColor : PenaltyColor;
    // Свободное место между названием теории и влиянием.
    var center = rect.Position + new Vector2(rect.Size.X / 2, rect.Size.Y * (HeaderShare + TheoryShare + 0.12f));
    var arrowHeight = rect.Size.Y * 0.14f;
    var arrowWidth = arrowHeight * 0.7f;
    var text = up ? $"+{modifier}%" : $"{modifier}%";
    var fontSize = (int)(rect.Size.Y * 0.11f);
    var textWidth = ThemeDB.FallbackFont.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X;
    var gap = rect.Size.X * 0.04f;
    var left = center.X - (arrowWidth + gap + textWidth) / 2;

    var arrowX = left + arrowWidth / 2;
    var top = center.Y - arrowHeight / 2;
    var bottom = top + arrowHeight;
    // Стрелка вниз: древко сверху, остриё внизу; стрелка вверх — наоборот.
    var tail = up ? bottom : top;
    var tip = up ? top : bottom;
    var headBase = up ? bottom - arrowHeight * 0.45f : top + arrowHeight * 0.45f;
    canvas.DrawLine(new Vector2(arrowX, tail), new Vector2(arrowX, headBase), color, arrowWidth * 0.35f);
    canvas.DrawColoredPolygon(
      [new Vector2(left, headBase), new Vector2(left + arrowWidth, headBase), new Vector2(arrowX, tip)],
      color);

    // Запас по ширине, чтобы надпись не переносилась из-за округления.
    var textRect = new Rect2(left + arrowWidth + gap, center.Y - fontSize / 2f, textWidth + 2, fontSize);
    DrawCenteredText(canvas, text, textRect, fontSize, color);
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
  /// Нарисовать закрытую карточку: рубашку с подписью и влиянием — по ним видно, что это гражданин без теории,
  /// но не видно, какая карточка лежит на самом деле.
  /// </summary>
  /// <param name="canvas">Где рисовать.</param>
  /// <param name="rect">Прямоугольник карточки.</param>
  /// <param name="card">Закрытая карточка.</param>
  public static void DrawHiddenBack(CanvasItem canvas, Rect2 rect, HiddenCard card) =>
    DrawBack(canvas, rect, HiddenBackColor, $"{card.Name}\n\nВлияние: {card.Influence}");

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
  /// Нарисовать лёгкую тень поверх закрытой карточки (<see cref="HiddenCard"/>), лицо которой видит только
  /// владелец или подсмотревший: так видно, что для остальных она лежит рубашкой вверх.
  /// </summary>
  /// <param name="canvas">Где рисовать.</param>
  /// <param name="rect">Прямоугольник карточки.</param>
  public static void DrawPeekOverlay(CanvasItem canvas, Rect2 rect)
  {
    var overlay = new StyleBoxFlat { BgColor = PeekOverlayColor };
    overlay.SetCornerRadiusAll(CornerRadius(rect));
    canvas.DrawStyleBox(overlay, rect);
  }

  /// <summary>
  /// Нарисовать серую пелену поверх карточки закрытого штаба теории.
  /// </summary>
  /// <param name="canvas">Где рисовать.</param>
  /// <param name="rect">Прямоугольник карточки.</param>
  public static void DrawClosedOverlay(CanvasItem canvas, Rect2 rect)
  {
    var overlay = new StyleBoxFlat { BgColor = ClosedOverlayColor };
    overlay.SetCornerRadiusAll(CornerRadius(rect));
    canvas.DrawStyleBox(overlay, rect);
  }

  /// <summary>
  /// Нарисовать наклонную чёрно-жёлтую ленту «CLOSED» поперёк карточки закрытого штаба теории.
  /// </summary>
  /// <remarks>
  /// Меняет преобразование рисования: рисуйте ленту последней.
  /// </remarks>
  /// <param name="canvas">Где рисовать.</param>
  /// <param name="rect">Прямоугольник карточки.</param>
  public static void DrawClosedRibbon(CanvasItem canvas, Rect2 rect)
  {
    var length = rect.Size.X * 1.2f;
    var height = rect.Size.Y * 0.17f;
    var stripe = height * 0.6f;
    var skew = height * 0.6f;
    var band = new Rect2(-length / 2, -height / 2, length, height);

    canvas.DrawSetTransform(rect.GetCenter(), -Mathf.Pi / 6);
    canvas.DrawRect(band, HazardYellow);
    // Наклонные чёрные полосы; у краёв ленты обрезаем их по её концам.
    for (var x = band.Position.X; x < band.End.X + skew; x += stripe * 2)
    {
      Vector2[] points =
      [
        new(x, band.Position.Y),
        new(x + stripe, band.Position.Y),
        new(x + stripe - skew, band.End.Y),
        new(x - skew, band.End.Y),
      ];
      canvas.DrawColoredPolygon(points.Select(point => point with { X = Mathf.Clamp(point.X, band.Position.X, band.End.X) }).ToArray(), HazardBlack);
    }

    var plate = new Rect2(-length * 0.3f, -height * 0.36f, length * 0.6f, height * 0.72f);
    canvas.DrawRect(plate, HazardYellow);
    canvas.DrawRect(plate, HazardBlack, filled: false, width: 2);
    DrawCenteredText(canvas, "CLOSED", plate, (int)(plate.Size.Y * 0.75f), HazardBlack);
    canvas.DrawSetTransform(Vector2.Zero);
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
  /// Нарисовать одну строку по центру области из кусков разного цвета. Если строка не помещается по ширине, шрифт уменьшается.
  /// </summary>
  public static void DrawSegmentedText(CanvasItem canvas, IReadOnlyList<(string Text, Color Color)> segments, Rect2 area, int fontSize)
  {
    var font = ThemeDB.FallbackFont;
    var width = segments.Sum(segment => font.GetStringSize(segment.Text, fontSize: fontSize).X);
    while (fontSize > 1 && width > area.Size.X)
    {
      fontSize--;
      width = segments.Sum(segment => font.GetStringSize(segment.Text, fontSize: fontSize).X);
    }

    var x = area.Position.X + (area.Size.X - width) / 2;
    var baseline = area.Position.Y + (area.Size.Y - font.GetHeight(fontSize)) / 2 + font.GetAscent(fontSize);
    foreach (var (text, color) in segments)
    {
      canvas.DrawString(font, new Vector2(x, baseline), text, fontSize: fontSize, modulate: color);
      x += font.GetStringSize(text, fontSize: fontSize).X;
    }
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
