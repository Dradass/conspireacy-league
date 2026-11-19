using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Game.Cards;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Подсказки теорий (<see cref="TheoryMarkers"/>) над карточками стола, кроме карточек в штабах теорий:
/// над карточками рынка — над верхним краем, на карточках личных штабов — у верхнего края внутри карточки (сверху
/// вплотную лежат другие карточки штаба).
/// </summary>
public partial class CardTheoryHints : Node2D
{
  private const float MarketGap = 22f;

  /// <summary>
  /// Сессия игры: рынок, поле и игроки.
  /// </summary>
  public GameSession Session { get; init; } = null!;

  /// <summary>
  /// Стол: где лежат карточки и как они повёрнуты, и для кого показывать подсказки (<see cref="Board.HintsViewer"/>).
  /// </summary>
  public Board Board { get; init; } = null!;

  public override void _Process(double delta) => QueueRedraw();

  public override void _Draw()
  {
    var viewer = Board.HintsViewer;
    foreach (var (cell, card) in Session.Market.OpenCards)
      DrawMarkers(cell.ToVector2I(), TheoryMarkers.For(Session, viewer, card), -CardPainter.CardSize.Y / 2 - MarketGap);

    foreach (var zone in Session.Field.PlayerZones.Values)
    {
      foreach (var cell in zone.Cells)
      {
        // Сыгранная карточка лежит рубашкой вверх: её теории не видно.
        if (Session.Field.CardAt(cell) is not { } card || Session.Field.IsUsed(cell))
          continue;
        var markers = TheoryMarkers.For(Session, viewer, card);
        if (markers.Count == 0)
          continue;
        DrawSetTransformMatrix(Board.SlotTransform(cell.ToVector2I()));
        TheoryMarkers.DrawOnTopEdge(this, new Rect2(-CardPainter.CardSize / 2, CardPainter.CardSize), markers);
      }
    }

    DrawSetTransformMatrix(Transform2D.Identity);
  }

  private void DrawMarkers(Vector2I cell, IReadOnlyList<TheoryMarkers.Marker> markers, float y)
  {
    if (markers.Count == 0)
      return;
    // Рисуем в координатах карточки: на радиальном поле она повёрнута, и подсказка стоит над её верхом.
    DrawSetTransformMatrix(Board.SlotTransform(cell));
    TheoryMarkers.Draw(this, new Vector2(0, y), markers);
  }
}
