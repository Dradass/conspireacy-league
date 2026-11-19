using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Game.Ui;
using Godot;

namespace ConspiracyLeague.Game.Cards;

/// <summary>
/// Подсказки над карточкой, где ещё есть её теория (с точки зрения игрока <c>viewer</c>, саму карточку не считаем):
/// серая стрелка — у него в руке; зелёная стрелка — на поле штаб теории, и он его владелец; зелёный круг — штаб теории
/// на поле, но владелец другой; круг цвета соперника — такая теория в личном штабе этого соперника.
/// Рисуются над всеми открытыми карточками, кроме карточек в штабах теорий.
/// </summary>
public static class TheoryMarkers
{
  private const float Spacing = 30f;
  private const float StemWidth = 6f;
  private const float StemHeight = 10f;
  private const float HeadWidth = 22f;
  private const float HeadHeight = 14f;
  private const float CircleRadius = 8f;
  private const float OutlineWidth = 2f;
  private const float EdgeInset = 6f;

  private static readonly Color HandColor = new("8a8a8a");
  private static readonly Color FieldColor = new("4cc76a");
  private static readonly Color OutlineColor = UiStyle.Dark;

  /// <summary>
  /// Одна подсказка: стрелка или круг и цвет.
  /// </summary>
  public readonly record struct Marker(bool Arrow, Color Color);

  /// <summary>
  /// Высота ряда подсказок при масштабе 1: по ней ряд ставят над карточкой или на её верхний край.
  /// </summary>
  public const float Height = StemHeight + HeadHeight;

  /// <summary>
  /// Подсказки для карточки с точки зрения игрока.
  /// </summary>
  public static IReadOnlyList<Marker> For(GameSession session, Player viewer, Card card)
  {
    if (VisibleFace(card)?.Theory is not { } theory)
      return [];

    var markers = new List<Marker>();
    if (viewer.Hand.Cards.Any(handCard => !ReferenceEquals(handCard, card) && handCard.Theory == theory))
      markers.Add(new Marker(true, HandColor));
    if (session.Field.TheoryColumns.FirstOrDefault(column => column.Theory == theory) is { } column)
      markers.Add(new Marker(column.Owner == viewer, FieldColor));

    // Личные штабы соперников: только открытые карточки, которые видны всем (не сыгранные и не закрытые).
    foreach (var player in session.Players)
    {
      if (player == viewer || !session.Field.PlayerZones.TryGetValue(player, out var zone))
        continue;
      var hasTheory = zone.Cells.Any(cell =>
        session.Field.CardAt(cell) is { } other
        && !ReferenceEquals(other, card)
        && !session.Field.IsUsed(cell)
        && VisibleFace(other)?.Theory == theory);
      if (hasTheory)
        markers.Add(new Marker(false, PlayerPalette.MainOf(player.Color)));
    }

    return markers;
  }

  /// <summary>
  /// Лицевая сторона, которую видят все: у нераскрытой закрытой карточки её нет.
  /// </summary>
  private static Card? VisibleFace(Card card) => card switch
  {
    HiddenCard { IsRevealed: false } => null,
    HiddenCard hidden => hidden.Face,
    _ => card,
  };

  /// <summary>
  /// Нарисовать ряд подсказок у верхнего края карточки, внутри неё: рядом с карточкой может лежать другая.
  /// Масштаб — по ширине карточки относительно карточки стола.
  /// </summary>
  public static void DrawOnTopEdge(CanvasItem canvas, Rect2 card, IReadOnlyList<Marker> markers)
  {
    if (markers.Count == 0)
      return;
    var scale = card.Size.X / CardPainter.CardSize.X;
    var y = card.Position.Y + (Height / 2 + EdgeInset) * scale;
    Draw(canvas, new Vector2(card.GetCenter().X, y), markers, scale);
  }

  /// <summary>
  /// Нарисовать ряд подсказок с центром в точке (в текущих координатах холста).
  /// </summary>
  /// <param name="scale">Масштаб: для уменьшенных карточек окон интерфейса подсказки тоже меньше.</param>
  public static void Draw(CanvasItem canvas, Vector2 center, IReadOnlyList<Marker> markers, float scale = 1f)
  {
    var x = center.X - (markers.Count - 1) * Spacing * scale / 2;
    foreach (var (arrow, color) in markers)
    {
      var position = new Vector2(x, center.Y);
      if (arrow)
        DrawArrow(canvas, position, color, scale);
      else
      {
        canvas.DrawCircle(position, (CircleRadius + OutlineWidth) * scale, OutlineColor);
        canvas.DrawCircle(position, CircleRadius * scale, color);
      }

      x += Spacing * scale;
    }
  }

  /// <summary>
  /// Стрелка, указывающая вниз на карточку; <paramref name="center"/> — центр всей стрелки.
  /// </summary>
  private static void DrawArrow(CanvasItem canvas, Vector2 center, Color color, float scale)
  {
    var top = center.Y - Height * scale / 2;
    var stem = new Rect2(center.X - StemWidth * scale / 2, top, StemWidth * scale, StemHeight * scale);
    Vector2[] head =
    [
      new(center.X - HeadWidth * scale / 2, top + StemHeight * scale),
      new(center.X + HeadWidth * scale / 2, top + StemHeight * scale),
      new(center.X, top + Height * scale),
    ];
    // Тёмная обводка: стрелка видна и на светлой карточке.
    canvas.DrawRect(stem.Grow(OutlineWidth * scale), OutlineColor);
    canvas.DrawPolyline([.. head, head[0]], OutlineColor, OutlineWidth * 2 * scale, antialiased: true);
    canvas.DrawRect(stem, color);
    canvas.DrawColoredPolygon(head, color);
  }
}
