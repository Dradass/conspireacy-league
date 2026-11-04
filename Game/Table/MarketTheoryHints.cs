using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Game.Cards;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Подсказки над карточками рынка во время хода игрока: серая стрелка — такая теория есть у него в руке;
/// зелёная стрелка — такая теория на поле, и он её владелец; зелёный круг — теория на поле, но владелец другой.
/// </summary>
public partial class MarketTheoryHints : Node2D
{
  private const float Gap = 22f;
  private const float Spacing = 30f;
  private const float StemWidth = 6f;
  private const float StemHeight = 10f;
  private const float HeadWidth = 22f;
  private const float HeadHeight = 14f;
  private const float CircleRadius = 8f;

  private static readonly Color HandColor = new("8a8a8a");
  private static readonly Color FieldColor = new("4cc76a");

  /// <summary>
  /// Сессия игры: рынок, поле и рука игрока, который ходит.
  /// </summary>
  public GameSession Session { get; init; } = null!;

  public override void _Process(double delta) => QueueRedraw();

  public override void _Draw()
  {
    if (Session.Phase != GamePhase.PlayerTurns)
      return;

    var player = Session.CurrentPlayer;
    foreach (var (cell, card) in Session.Market.OpenCards)
    {
      if (card.Theory is not { } theory)
        continue;

      // Каждая подсказка: (стрелка или круг, цвет); стрелка для руки и для своей теории на поле.
      var markers = new List<(bool Arrow, Color Color)>();
      if (player.Hand.Cards.Any(handCard => handCard.Theory == theory))
        markers.Add((true, HandColor));
      if (Session.Field.TheoryColumns.FirstOrDefault(column => column.Theory == theory) is { } column)
        markers.Add((column.Owner == player, FieldColor));

      var center = Board.CellCenter(cell.ToVector2I());
      var y = center.Y - CardPainter.CardSize.Y / 2 - Gap;
      var x = center.X - (markers.Count - 1) * Spacing / 2;
      foreach (var (arrow, color) in markers)
      {
        var position = new Vector2(x, y);
        if (arrow)
          DrawArrow(position, color);
        else
          DrawCircle(position, CircleRadius, color);
        x += Spacing;
      }
    }
  }

  /// <summary>
  /// Стрелка, указывающая вниз на карточку; <paramref name="center"/> — центр всей стрелки.
  /// </summary>
  private void DrawArrow(Vector2 center, Color color)
  {
    var top = center.Y - (StemHeight + HeadHeight) / 2;
    DrawRect(new Rect2(center.X - StemWidth / 2, top, StemWidth, StemHeight), color);
    DrawColoredPolygon(
    [
      new Vector2(center.X - HeadWidth / 2, top + StemHeight),
      new Vector2(center.X + HeadWidth / 2, top + StemHeight),
      new Vector2(center.X, top + StemHeight + HeadHeight),
    ], color);
  }
}
