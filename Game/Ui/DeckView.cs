using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Game.Cards;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Колода в интерфейсе: стопка карточек рубашкой вверх, из которой можно вытянуть верхнюю.
/// </summary>
public partial class DeckView : Control
{
  private const int MaxVisibleLayers = 5;
  private const float LayerOffset = 3f;
  private const float LabelHeight = 24f;

  /// <summary>
  /// Карточки в колоде мельче, чем на столе: колоды не должны закрывать зону игрока.
  /// </summary>
  private static readonly Vector2 DeckCardSize = CardPainter.CardSize * 0.75f;

  private static readonly Color EmptyColor = new(1, 1, 1, 0.35f);
  private static readonly Color LabelColor = new("fdfaf2");

  private readonly Stack<Card> _cards = new();
  private string _title = string.Empty;
  private Color _accent;

  /// <summary>
  /// Игрок начал тянуть верхнюю карточку колоды. Карточка уже снята с колоды.
  /// </summary>
  public event Action<DeckView, Card>? CardDragStarted;

  /// <summary>
  /// Центр верхней карточки в координатах слоя интерфейса.
  /// </summary>
  public Vector2 TopCardCenter => GetGlobalTransform() * TopCardRect().GetCenter();

  /// <summary>
  /// Создать колоду.
  /// </summary>
  /// <param name="title">Название колоды.</param>
  /// <param name="cards">Карточки; первая окажется сверху.</param>
  public static DeckView Create(string title, IReadOnlyList<Card> cards)
  {
    var deck = new DeckView
    {
      Name = title,
      _title = title,
      _accent = cards.Count > 0 ? CardPainter.AccentOf(cards[0]) : EmptyColor,
      MouseFilter = MouseFilterEnum.Stop,
      CustomMinimumSize = DeckCardSize + new Vector2(LayerOffset, LayerOffset) * (MaxVisibleLayers - 1) + new Vector2(0, LabelHeight),
    };
    for (var i = cards.Count - 1; i >= 0; i--)
      deck.Push(cards[i]);

    return deck;
  }

  /// <summary>
  /// Положить карточку на верх колоды.
  /// </summary>
  public void Push(Card card)
  {
    _cards.Push(card);
    Refresh();
  }

  public override void _GuiInput(InputEvent @event)
  {
    if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } || _cards.Count == 0)
      return;

    AcceptEvent();
    var card = _cards.Pop();
    Refresh();
    CardDragStarted?.Invoke(this, card);
  }

  public override void _Draw()
  {
    var layers = Mathf.Min(_cards.Count, MaxVisibleLayers);
    if (layers == 0)
    {
      var outline = new StyleBoxFlat { DrawCenter = false, BorderColor = EmptyColor };
      outline.SetBorderWidthAll(2);
      outline.SetCornerRadiusAll(8);
      DrawStyleBox(outline, LayerRect(0));
      CardPainter.DrawCenteredText(this, "Пусто", LayerRect(0), 14, EmptyColor);
    }

    for (var i = 0; i < layers; i++)
      CardPainter.DrawBack(this, LayerRect(i), _accent, i == layers - 1 ? _title : null);

    var labelRect = new Rect2(0, Size.Y - LabelHeight, Size.X, LabelHeight);
    CardPainter.DrawCenteredText(this, $"Карт: {_cards.Count}", labelRect, 14, LabelColor);
  }

  private void Refresh()
  {
    MouseDefaultCursorShape = _cards.Count > 0 ? CursorShape.PointingHand : CursorShape.Arrow;
    QueueRedraw();
  }

  /// <summary>
  /// Прямоугольник слоя стопки: 0 — нижний, каждый следующий сдвинут вправо-вверх.
  /// </summary>
  private static Rect2 LayerRect(int layer)
  {
    var bottom = new Vector2(0, LayerOffset * (MaxVisibleLayers - 1));
    return new Rect2(bottom + new Vector2(LayerOffset, -LayerOffset) * layer, DeckCardSize);
  }

  private Rect2 TopCardRect() => LayerRect(Mathf.Max(Mathf.Min(_cards.Count, MaxVisibleLayers) - 1, 0));
}
