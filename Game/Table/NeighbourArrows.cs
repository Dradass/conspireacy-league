using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Зелёные стрелки над местами слева и справа от семейства теорий: туда можно положить дочернюю теорию
/// (во время перетаскивания карточки). Стрелка смотрит на семейство.
/// </summary>
public partial class NeighbourArrows : Node2D
{
  private const float Width = 0.3f;
  private const float Height = 0.22f;

  private static readonly Color ArrowColor = new("4cc76a");

  private IReadOnlyList<(Vector2I Cell, bool ToLeft)> _arrows = [];

  /// <summary>
  /// Показать стрелки на перечисленных ячейках.
  /// </summary>
  public void Show(IReadOnlyList<(Vector2I Cell, bool ToLeft)> arrows)
  {
    _arrows = arrows;
    QueueRedraw();
  }

  public override void _Draw()
  {
    var size = Board.CellSize;
    foreach (var (cell, toLeft) in _arrows)
    {
      var center = Board.CellCenter(cell);
      var half = new Vector2(size.X * Width, size.Y * Height) / 2;
      // Слева от семейства стрелка смотрит вправо, справа — влево.
      var tip = toLeft ? center.X + half.X : center.X - half.X;
      var back = toLeft ? center.X - half.X : center.X + half.X;
      DrawColoredPolygon(
      [
        new Vector2(tip, center.Y),
        new Vector2(back, center.Y - half.Y),
        new Vector2(back, center.Y + half.Y),
      ], ArrowColor);
    }
  }
}
