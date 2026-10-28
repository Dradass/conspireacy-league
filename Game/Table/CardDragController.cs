using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Game.Cards;
using ConspiracyLeague.Game.Ui;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Перетаскивание карточек: из колоды на стол и между ячейками стола.
/// </summary>
/// <remarks>
/// Карточку можно положить только в свободную ячейку сетки. Иначе (или по Esc)
/// она возвращается туда, откуда её взяли.
/// </remarks>
public partial class CardDragController : Node
{
  private const double ReturnDuration = 0.2;

  private Board _board = null!;
  private CanvasLayer _dragLayer = null!;
  private Drag? _drag;

  /// <summary>
  /// Перетаскиваемая карточка и откуда её взяли.
  /// </summary>
  /// <param name="View">Карточка.</param>
  /// <param name="GrabOffset">Смещение центра карточки относительно курсора.</param>
  /// <param name="SourceDeck">Колода, из которой взяли карточку.</param>
  /// <param name="SourceCell">Ячейка, из которой взяли карточку.</param>
  private sealed record Drag(CardView View, Vector2 GrabOffset, DeckView? SourceDeck, Vector2I? SourceCell);

  /// <summary>
  /// Настроить контроллер.
  /// </summary>
  /// <param name="board">Стол.</param>
  /// <param name="dragLayer">Слой поверх интерфейса, следующий за камерой: в нём летит карточка.</param>
  /// <param name="decks">Колоды, из которых можно тянуть карточки.</param>
  public void Initialize(Board board, CanvasLayer dragLayer, IEnumerable<DeckView> decks)
  {
    _board = board;
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

  public override void _UnhandledInput(InputEvent @event)
  {
    if (_drag is not null || @event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
      return;

    var mouse = MouseOnTable();
    if (_board.CellAt(mouse) is not { } cell || _board.Take(cell) is not { } view)
      return;

    _dragLayer.AddChild(view);
    Begin(new Drag(view, view.Position - mouse, null, cell));
    GetViewport().SetInputAsHandled();
  }

  public override void _Process(double delta)
  {
    // Обновляем каждый кадр, а не только при движении мыши: камера может сдвинуться под карточкой.
    if (_drag is null)
      return;

    _drag.View.Position = MouseOnTable() + _drag.GrabOffset;
    _board.Highlight(_board.CellAt(_drag.View.Position));
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
    _dragLayer.AddChild(view);
    Begin(new Drag(view, Vector2.Zero, deck, null));
  }

  private void Begin(Drag drag)
  {
    _drag = drag;
    drag.View.Lifted = true;
    Input.SetDefaultCursorShape(Input.CursorShape.Drag);
  }

  private void Drop()
  {
    if (End() is not { } drag)
      return;

    if (_board.CellAt(drag.View.Position) is { } cell && _board.IsFree(cell))
    {
      _dragLayer.RemoveChild(drag.View);
      _board.Place(drag.View, cell);
    }
    else
      ReturnToSource(drag);
  }

  private void Cancel()
  {
    if (End() is { } drag)
      ReturnToSource(drag);
  }

  private Drag? End()
  {
    var drag = _drag;
    _drag = null;
    _board.Highlight(null);
    Input.SetDefaultCursorShape();
    return drag;
  }

  private void ReturnToSource(Drag drag)
  {
    var view = drag.View;
    if (drag.SourceCell is { } cell)
    {
      _dragLayer.RemoveChild(view);
      _board.Place(view, cell);
      return;
    }

    if (drag.SourceDeck is not { } deck)
      return;

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

  private Vector2 MouseOnTable() => _board.GetGlobalMousePosition();
}
