using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Game.Cards;
using ConspiracyLeague.Game.Ui;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Перетаскивание карточек: с рынка в руку и из руки на стол.
/// </summary>
/// <remarks>
/// Карточку рынка можно перетащить в руку, если на неё хватает денег. Карточку из руки можно положить
/// в свою рабочую зону (личный штаб) или в общую зону — куда именно, решает <see cref="PlayingField"/>.
/// Если туда нельзя (или нажат Esc), карточка возвращается, откуда её взяли. Выложенные карточки больше не перемещаются.
/// </remarks>
public partial class CardDragController : Node
{
  private const double ReturnDuration = 0.2;

  private Board _board = null!;
  private GameSession _session = null!;
  private CanvasLayer _dragLayer = null!;
  private HandView _hand = null!;
  private Drag? _drag;

  /// <summary>
  /// Карточку не дали положить; аргумент — причина.
  /// </summary>
  public event Action<string>? PlacementDenied;

  /// <summary>
  /// Перетаскиваемая карточка.
  /// </summary>
  /// <param name="View">Карточка.</param>
  private abstract record Drag(CardView View);

  /// <summary>
  /// Карточка, взятая из руки.
  /// </summary>
  /// <param name="View">Карточка.</param>
  /// <param name="Source">Карточка в руке; пока её тащат, она скрыта.</param>
  private sealed record HandDrag(CardView View, HandCardView Source) : Drag(View);

  /// <summary>
  /// Карточка, взятая с рынка.
  /// </summary>
  /// <param name="View">Карточка.</param>
  /// <param name="Cell">Ячейка рынка, где она лежала.</param>
  private sealed record MarketDrag(CardView View, Vector2I Cell) : Drag(View);

  /// <summary>
  /// Настроить контроллер.
  /// </summary>
  /// <param name="board">Стол.</param>
  /// <param name="session">Сессия игры: карточки покупает и выкладывает игрок, который сейчас ходит.</param>
  /// <param name="dragLayer">Слой поверх интерфейса, следующий за камерой: в нём летит карточка.</param>
  /// <param name="hand">Рука игрока, который сейчас ходит.</param>
  public void Initialize(Board board, GameSession session, CanvasLayer dragLayer, HandView hand)
  {
    _board = board;
    _session = session;
    _dragLayer = dragLayer;
    _hand = hand;
    _hand.CardDragStarted += OnHandCardDragStarted;
  }

  public override void _ExitTree() => _hand.CardDragStarted -= OnHandCardDragStarted;

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
    if (_drag is not null || !CanAct || @event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
      return;
    if (_board.CellAt(MouseOnTable()) is not { } cell || _board.MarketViewAt(cell) is null)
      return;

    GetViewport().SetInputAsHandled();
    var view = _board.TakeMarketView(cell);
    StartDrag(new MarketDrag(view, cell));
    _hand.DropState = HandDropState.Target;
    // Пока решаем, брать ли карточку, подсвечиваем её теорию на поле.
    _board.ShowTargets(_board.Field.TheoryCellsOf(view.Card));
  }

  public override void _Process(double delta)
  {
    // Обновляем каждый кадр, а не только при движении мыши: камера может сдвинуться под карточкой.
    if (_drag is null)
      return;

    _drag.View.Position = MouseOnTable();
    switch (_drag)
    {
      case HandDrag:
        var cell = _board.CellAt(_drag.View.Position);
        _board.Highlight(cell, cell is { } target ? Check(_drag.View, target) : null);
        break;
      case MarketDrag:
        _hand.DropState = _hand.ContainsScreenPoint(MouseOnScreen()) ? HandDropState.Hover : HandDropState.Target;
        break;
    }
  }

  private void OnHandCardDragStarted(HandCardView source)
  {
    if (_drag is not null || !CanAct)
      return;

    // Карточка остаётся в руке, пока её не выложат; на время перетаскивания прячем её.
    source.Modulate = Colors.Transparent;
    StartDrag(new HandDrag(CardView.Create(source.Card), source));
    _board.ShowAvailable(_board.Field.AvailableCells(source.Card, _session.CurrentPlayer));
    _board.ShowArrows(_board.Field.ArrowTargets(source.Card, _session.CurrentPlayer));
  }

  private void StartDrag(Drag drag)
  {
    drag.View.Position = MouseOnTable();
    drag.View.Lifted = true;
    _dragLayer.AddChild(drag.View);
    _drag = drag;
    Input.SetDefaultCursorShape(Input.CursorShape.Drag);
  }

  private void Drop()
  {
    switch (End())
    {
      case HandDrag drag:
        DropFromHand(drag);
        break;
      case MarketDrag drag:
        DropFromMarket(drag);
        break;
    }
  }

  private void DropFromHand(HandDrag drag)
  {
    if (_board.CellAt(drag.View.Position) is not { } cell)
    {
      ReturnToHand(drag);
      return;
    }

    var check = Check(drag.View, cell);
    if (!check.IsAllowed)
    {
      PlacementDenied?.Invoke(check.Reason!);
      ReturnToHand(drag);
      return;
    }

    // Соседнюю карточку семейства теорий не занимают: над ней карточка вставляется, а она и её сторона сдвигаются.
    if (_board.Field.NeighbourBoundary(drag.View.Card, cell.ToCell()) is { } boundary)
    {
      DropInWall(drag, boundary);
      return;
    }

    var player = _session.CurrentPlayer;
    var placed = _session.PlaceFromHand(drag.View.Card, cell.ToCell());
    // Действие при выкладывании могло убрать карточки под выложенной — тогда она опустилась ниже.
    if (_board.Field.CellOf(drag.View.Card) is { } actual)
      cell = actual.ToVector2I();
    drag.View.IsHeadquarters = placed.CreatesHeadquarters;
    // Карточка в теории отмечается миплом цвета владельца.
    if (_board.Field.CommonZone.Contains(cell.ToCell()))
      drag.View.CardOwner = player;
    _dragLayer.RemoveChild(drag.View);
    _board.Place(drag.View, cell);
  }

  /// <summary>
  /// Дочерняя теория вставлена в перегородку: теории слева сдвигаются, карточка ложится в освободившийся столбец.
  /// </summary>
  private void DropInWall(HandDrag drag, int boundary)
  {
    var player = _session.CurrentPlayer;
    var placed = _session.PlaceInWallFromHand(drag.View.Card, boundary);
    var cell = _board.Field.CellOf(drag.View.Card)!.Value.ToVector2I();
    drag.View.IsHeadquarters = placed.CreatesHeadquarters;
    drag.View.CardOwner = player;
    _dragLayer.RemoveChild(drag.View);
    _board.Place(drag.View, cell);
  }

  private void DropFromMarket(MarketDrag drag)
  {
    if (!_hand.ContainsScreenPoint(MouseOnScreen()))
    {
      ReturnToMarket(drag);
      return;
    }

    var check = _session.Market.CheckBuy(drag.Cell.ToCell(), _session.CurrentPlayer);
    if (!check.IsAllowed)
    {
      PlacementDenied?.Invoke(check.Reason!);
      ReturnToMarket(drag);
      return;
    }

    // Купленная карточка появляется в руке, летящая карточка больше не нужна.
    _session.BuyFromMarket(drag.Cell.ToCell());
    drag.View.QueueFree();
  }

  private void Cancel()
  {
    switch (End())
    {
      case HandDrag drag:
        ReturnToHand(drag);
        break;
      case MarketDrag drag:
        ReturnToMarket(drag);
        break;
    }
  }

  private Drag? End()
  {
    var drag = _drag;
    _drag = null;
    _board.Highlight(null);
    _board.HideArrows();
    _board.HideAvailable();
    _hand.DropState = HandDropState.None;
    Input.SetDefaultCursorShape();
    return drag;
  }

  private void ReturnToHand(HandDrag drag)
  {
    var view = drag.View;
    var source = drag.Source;
    // Рука живёт в координатах экрана, карточка — в координатах стола.
    var target = GetViewport().GetCanvasTransform().AffineInverse() * source.Center;
    var tween = view.CreateTween();
    tween.TweenProperty(view, Node2D.PropertyName.Position.ToString(), target, ReturnDuration)
      .SetTrans(Tween.TransitionType.Cubic)
      .SetEase(Tween.EaseType.Out);
    tween.TweenCallback(Callable.From(() =>
    {
      view.QueueFree();
      if (IsInstanceValid(source))
        source.Modulate = Colors.White;
    }));
  }

  private void ReturnToMarket(MarketDrag drag)
  {
    _dragLayer.RemoveChild(drag.View);
    _board.ReturnMarketView(drag.View, drag.Cell);
  }

  private PlacementCheck Check(CardView view, Vector2I cell) => _board.Field.CheckDrop(view.Card, cell.ToCell(), _session.CurrentPlayer);

  /// <summary>
  /// Брать карточки можно только в фазе ходов игроков.
  /// </summary>
  private bool CanAct =>
    _session.Phase == GamePhase.PlayerTurns && _session.PendingCheck is null && _session.PendingPlacement is null;

  private Vector2 MouseOnTable() => _board.GetGlobalMousePosition();

  private Vector2 MouseOnScreen() => GetViewport().GetMousePosition();
}
