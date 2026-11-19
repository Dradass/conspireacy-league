using ConspiracyLeague.Game.Ui;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Подсветка мест для перетаскиваемой карточки поверх карточек стола: доступные места, где уже лежит карточка
/// (дочерняя теория встаёт на место другой теории и сдвигает её), и свободные места сверху стопок теорий, куда карточку
/// без теории можно положить за плату, — зелёным, с суммой; если денег на такое место сейчас не хватает — красным.
/// </summary>
public partial class PlacementOverlay : Node2D
{
  private const float Inset = 4f;
  private const float FillAlpha = 0.3f;
  private const float BorderAlpha = 0.9f;
  private const float BorderWidth = 3f;
  private const int PriceFontSize = 22;
  private const int PriceOutline = 6;

  private static readonly Color PlaceColor = new(0.55f, 0.95f, 0.6f);
  private static readonly Color PriceColor = UiStyle.Glow;
  private static readonly Color LackColor = new(1f, 0.35f, 0.3f);

  private IReadOnlyList<Vector2I> _covered = [];
  private IReadOnlyList<(Vector2I Cell, int Cost, bool Affordable)> _joinSlots = [];

  /// <summary>
  /// Стол: где лежат ячейки и как они повёрнуты.
  /// </summary>
  public Board Board { get; set; } = null!;

  /// <summary>
  /// Доступные места, на которых уже лежит карточка.
  /// </summary>
  public void ShowCovered(IReadOnlyList<Vector2I> cells)
  {
    _covered = cells;
    QueueRedraw();
  }

  /// <summary>
  /// Места сверху стопок теорий, куда можно присоединить карточку без теории, и цена каждого;
  /// <c>Affordable</c> = <c>false</c> — денег сейчас не хватает (место красное).
  /// </summary>
  public void SetJoinSlots(IReadOnlyList<(Vector2I Cell, int Cost, bool Affordable)> slots)
  {
    _joinSlots = slots;
    QueueRedraw();
  }

  public override void _Draw()
  {
    var slot = new Rect2(-Board.CellSize / 2, Board.CellSize).Grow(-Inset);
    foreach (var cell in _covered)
    {
      DrawSetTransformMatrix(Board.SlotTransform(cell));
      DrawSlot(slot, PlaceColor);
    }

    // Цифры — шрифтом чисел интерфейса, зелёным свечением с тёмной обводкой, как остальной интерфейс;
    // если денег не хватает — место и цена красные.
    var font = UiStyle.NumberFont;
    foreach (var (cell, cost, affordable) in _joinSlots)
    {
      DrawSetTransformMatrix(Board.SlotTransform(cell));
      DrawSlot(slot, affordable ? PlaceColor : LackColor);
      var text = cost > 0 ? $"−{cost}$" : "0$";
      var position = new Vector2(slot.Position.X, PriceFontSize / 3f);
      DrawStringOutline(font, position, text, HorizontalAlignment.Center, slot.Size.X, PriceFontSize, PriceOutline, UiStyle.Dark);
      DrawString(font, position, text, HorizontalAlignment.Center, slot.Size.X, PriceFontSize, affordable ? PriceColor : LackColor);
    }

    DrawSetTransformMatrix(Transform2D.Identity);
  }

  private void DrawSlot(Rect2 slot, Color color)
  {
    DrawRect(slot, color with { A = FillAlpha });
    DrawRect(slot.Grow(-BorderWidth / 2), color with { A = BorderAlpha }, filled: false, width: BorderWidth);
  }
}
