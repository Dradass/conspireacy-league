using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Финишная линия режима гонки в общей зоне: клетчатая полоса по верхнему краю карточки, которая ляжет в штаб теории
/// <see cref="GameSession.FinishLeaders"/>-й по счёту. Дочерние теории лежат на уровне третьей карточки родительской,
/// поэтому линия для них та же — им нужно на два лидера меньше. Рисуется под карточками.
/// </summary>
public partial class FinishLine : Node2D
{
  /// <summary>
  /// Сторона клетки полосы, пикселей.
  /// </summary>
  private const float Square = 10f;

  private const int LabelFontSize = 22;

  private static readonly Color Light = new("f2f2f2");
  private static readonly Color Dark = new("1c1c1c");
  private static readonly Color LabelColor = new("e0b84f");

  /// <summary>
  /// Сессия игры: количество игроков (длина гонки) и общая зона.
  /// </summary>
  public GameSession Session { get; init; } = null!;

  /// <summary>
  /// Стол: где лежит общая зона.
  /// </summary>
  public Board Board { get; init; } = null!;

  public override void _Process(double delta) => QueueRedraw();

  public override void _Draw()
  {
    var row = Board.CommonCardTopRow(Session.FinishLeaders);
    var half = Square / Board.CellSize.Y;
    if (!Board.IsCommonRowOnTable(row - half))
      return;

    var zone = Session.Field.CommonZone;
    var left = zone.Left - 0.5f;
    var right = zone.Left + zone.Width - 0.5f;
    var length = Board.CommonPoint(left, row).DistanceTo(Board.CommonPoint(right, row));
    // На радиальном поле полоса — дуга: длину считаем по ломаной из мелких отрезков.
    if (Board.Radial)
    {
      length = 0;
      const int probes = 64;
      for (var i = 0; i < probes; i++)
      {
        length += Board.CommonPoint(Mathf.Lerp(left, right, (float)i / probes), row)
          .DistanceTo(Board.CommonPoint(Mathf.Lerp(left, right, (float)(i + 1) / probes), row));
      }
    }

    var count = Mathf.Max(1, Mathf.CeilToInt(length / Square));
    for (var i = 0; i < count; i++)
    {
      var from = Mathf.Lerp(left, right, (float)i / count);
      var to = Mathf.Lerp(left, right, (float)(i + 1) / count);
      for (var band = 0; band < 2; band++)
      {
        var top = row - half + band * half;
        var bottom = top + half;
        var color = (i + band) % 2 == 0 ? Dark : Light;
        DrawColoredPolygon(
          [Board.CommonPoint(from, top), Board.CommonPoint(to, top), Board.CommonPoint(to, bottom), Board.CommonPoint(from, bottom)],
          color);
      }
    }

    var font = ThemeDB.FallbackFont;
    var text = $"Финиш: {Session.FinishLeaders} лидеров (побочная теория — {Session.FinishLeaders - (PlayingField.SideTheoryLevel - 1)})";
    var anchor = Board.CommonPoint(left, row - half) + new Vector2(0, -6);
    DrawString(font, anchor, text, HorizontalAlignment.Left, -1, LabelFontSize, LabelColor);
  }
}
