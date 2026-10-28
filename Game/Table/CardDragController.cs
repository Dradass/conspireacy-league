using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Game.Cards;
using ConspiracyLeague.Game.Ui;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Перетаскивание карточек из колоды на стол.
/// </summary>
/// <remarks>
/// Куда можно положить карточку, решает <see cref="PlayingField"/>. Если туда нельзя (или нажат Esc),
/// карточка возвращается в колоду. Выложенные карточки больше не перемещаются.
/// </remarks>
public partial class CardDragController : Node
{
  private const double ReturnDuration = 0.2;

  private Board _board = null!;
  private Player _player = null!;
  private CanvasLayer _dragLayer = null!;
  private Drag? _drag;

  /// <summary>
  /// Карточку не дали положить; аргумент — причина.
  /// </summary>
  public event Action<string>? PlacementDenied;

  /// <summary>
  /// Перетаскиваемая карточка и откуда её взяли.
  /// </summary>
  /// <param name="View">Карточка.</param>
  /// <param name="SourceDeck">Колода, из которой взяли карточку.</param>
  private sealed record Drag(CardView View, DeckView SourceDeck);

  /// <summary>
  /// Настроить контроллер.
  /// </summary>
  /// <param name="board">Стол.</param>
  /// <param name="player">Игрок, который выкладывает карточки.</param>
  /// <param name="dragLayer">Слой поверх интерфейса, следующий за камерой: в нём летит карточка.</param>
  /// <param name="decks">Колоды, из которых можно тянуть карточки.</param>
  public void Initialize(Board board, Player player, CanvasLayer dragLayer, IEnumerable<DeckView> decks)
  {
    _board = board;
    _player = player;
    _dragLayer = dragLayer;
    foreach (var deck in decks)
      deck.CardDragStarted += OnDeckCardDragStarted;
  }

  public override void _Input(InputEvent @event)
  {
    if (_drag is null)
      return;

    // Отпускание не помечаем обработанным: интерфейс должен узнать, что кнопка отпущена.
    if (@event is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left })
      Drop();
    else if (@event.IsActionPressed("ui_cancel"))
    {
      Cancel();
      GetViewport().SetInputAsHandled();
    }
  }

  public override void _Process(double delta)
  {
    // Обновляем каждый кадр, а не только при движении мыши: камера может сдвинуться под карточкой.
    if (_drag is null)
      return;

    _drag.View.Position = MouseOnTable();
    var cell = _board.CellAt(_drag.View.Position);
    _board.Highlight(cell, cell is { } target ? Check(_drag.View.Card, target) : null);
  }

  private void OnDeckCardDragStarted(DeckView deck, Card card)
  {
    if (_drag is not null)
    {
      deck.Push(card);
      return;
    }

    var view = CardView.Create(card);
    view.Position = MouseOnTable();
    view.Lifted = true;
    _dragLayer.AddChild(view);
    _drag = new Drag(view, deck);
    _board.ShowAvailable(_board.Field.AvailableCells(card, _player));
    Input.SetDefaultCursorShape(Input.CursorShape.Drag);
  }

  private void Drop()
  {
    if (End() is not { } drag)
      return;

    if (_board.CellAt(drag.View.Position) is not { } cell)
    {
      ReturnToDeck(drag);
      return;
    }

    var check = Check(drag.View.Card, cell);
    if (!check.IsAllowed)
    {
      PlacementDenied?.Invoke(check.Reason!);
      ReturnToDeck(drag);
      return;
    }

    drag.View.IsHeadquarters = _board.Field.Place(drag.View.Card, cell.ToCell(), _player).CreatesHeadquarters;
    _dragLayer.RemoveChild(drag.View);
    _board.Place(drag.View, cell);
  }

  private void Cancel()
  {
    if (End() is { } drag)
      ReturnToDeck(drag);
  }

  private Drag? End()
  {
    var drag = _drag;
    _drag = null;
    _board.Highlight(null);
    _board.HideAvailable();
    Input.SetDefaultCursorShape();
    return drag;
  }

  private void ReturnToDeck(Drag drag)
  {
    var view = drag.View;
    var deck = drag.SourceDeck;
    // Колода живёт в координатах экрана, карточка — в координатах стола.
    var target = GetViewport().GetCanvasTransform().AffineInverse() * deck.TopCardCenter;
    var tween = view.CreateTween();
    tween.TweenProperty(view, "position", target, ReturnDuration)
      .SetTrans(Tween.TransitionType.Cubic)
      .SetEase(Tween.EaseType.Out);
    tween.TweenCallback(Callable.From(() =>
    {
      view.QueueFree();
      deck.Push(view.Card);
    }));
  }

  private PlacementCheck Check(Card card, Vector2I cell) => _board.Field.Check(card, cell.ToCell(), _player);

  private Vector2 MouseOnTable() => _board.GetGlobalMousePosition();
}
