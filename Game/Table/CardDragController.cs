using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Commands;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Game.Cards;
using ConspiracyLeague.Game.Net;
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
  private HandHub _hand = null!;
  private PersonalHqHub _personalHq = null!;
  private MarketHub _market = null!;
  private Drag? _drag;

  /// <summary>
  /// Меню «открыто или взакрытую»: карточку бросили на теорию, куда игрок может выкладывать взакрытую
  /// (право Спецагента, <see cref="PlayingField.AllowsHiddenPlacement"/>).
  /// </summary>
  private PopupMenu _placementMenu = null!;

  /// <summary>
  /// Карточка ждёт выбора в <see cref="_placementMenu"/>: куда её класть открыто (<c>null</c> — открыто нельзя)
  /// и в какую теорию — взакрытую.
  /// </summary>
  private HiddenChoice? _hiddenChoice;

  private sealed record HiddenChoice(HandDrag Drag, Vector2I? OpenCell, TheoryColumn Column);

  private const int OpenPlacementId = 0;
  private const int HiddenPlacementId = 1;

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
  /// <param name="hand">Окна руки игрока, который сейчас ходит.</param>
  /// <param name="personalHq">Окна личного штаба: в свой штаб тоже можно положить карточку из руки.</param>
  /// <param name="market">Окна рынка: из них, как и со стола, карточку рынка можно перетащить в руку.</param>
  /// <summary>
  /// Игра через сервер: карточки не кладутся здесь, а уходят на сервер командами; когда сервер примет команду, партия
  /// уже обновлена, и летящая карточка ложится на своё место. <c>null</c> — игра за одним компьютером.
  /// </summary>
  public NetworkGame? Net { get; set; }

  /// <summary>
  /// Игра через сервер: отправить команду выкладывания; принята — летящая карточка ложится туда, где карточка теперь
  /// лежит в партии, нет — возвращается в руку.
  /// </summary>
  private void SendPlacement(NetworkGame net, HandDrag drag, GameCommand command) =>
    net.Send(command, accepted =>
    {
      if (accepted && _board.Field.CellOf(drag.View.Card) is not null)
        SettleDrag(drag);
      else if (drag.View.GetParent() == _dragLayer)
        ReturnToHand(drag);
      if (accepted && _hand.DraggedCard == drag.View.Card)
        _hand.DraggedCard = null;
    });

  public void Initialize(Board board, GameSession session, CanvasLayer dragLayer, HandHub hand, PersonalHqHub personalHq, MarketHub market)
  {
    _personalHq = personalHq;
    _market = market;
    _market.CardDragStarted += OnMarketCardDragStarted;
    _board = board;
    _session = session;
    _dragLayer = dragLayer;
    _hand = hand;
    _hand.CardDragStarted += OnHandCardDragStarted;
  }

  public override void _Ready()
  {
    _placementMenu = new PopupMenu { Name = "PlacementMenu" };
    _placementMenu.IdPressed += OnPlacementMenuIdPressed;
    // Меню закрыли, ничего не выбрав (Esc, щелчок мимо): карточка возвращается в руку. Откладываем, чтобы сначала
    // успел сработать выбор пункта.
    _placementMenu.PopupHide += () => Callable.From(CancelHiddenChoice).CallDeferred();
    AddChild(_placementMenu);
  }

  public override void _ExitTree()
  {
    _hand.CardDragStarted -= OnHandCardDragStarted;
    _market.CardDragStarted -= OnMarketCardDragStarted;
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
    if (_drag is not null || !CanAct || @event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
      return;
    if (_board.CellAt(MouseOnTable()) is not { } cell || _board.MarketViewAt(cell) is null)
      return;

    GetViewport().SetInputAsHandled();
    StartMarketDrag(cell);
  }

  /// <summary>
  /// Карточку рынка потянули из окна рынка.
  /// </summary>
  private void OnMarketCardDragStarted(Vector2I cell)
  {
    if (_drag is null && CanAct && _board.MarketViewAt(cell) is not null)
      StartMarketDrag(cell);
  }

  private void StartMarketDrag(Vector2I cell)
  {
    var view = _board.TakeMarketView(cell);
    StartDrag(new MarketDrag(view, cell));
    _market.DraggedCell = cell;
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
        var cell = TargetCell(_drag.View);
        var check = cell is { } target ? DropCheck(_drag.View, target) : null;
        _board.Highlight(cell, check);
        // Места присоединения к теориям с ценой видны, пока курсор над зоной штабов теорий.
        _board.SetJoinSlotsVisible(_board.IsInCommonZone(MouseOnTable()));
        var hqCell = _personalHq.CellAtScreenPoint(MouseOnScreen());
        _personalHq.Highlight(hqCell, check?.IsAllowed ?? false);
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

    // Карточка остаётся в руке, пока её не выложат; на время перетаскивания прячем её во всех окнах руки.
    _hand.DraggedCard = source.Card;
    StartDrag(new HandDrag(CardView.Create(source.Card), source));
    var available = _board.Field.AvailableCells(source.Card, _session.CurrentPlayer).ToList();
    // Карточку без теории можно присоединить сверху к любой теории за плату: эти места подсвечиваются отдельно —
    // зелёным с суммой, только пока курсор над зоной штабов теорий.
    var joinSlots = source.Card is PersonCard { Theory: null }
      ? available.Where(pair => pair.Check is { IsAllowed: true, Column: { } column } && column.Top.Above == pair.Cell).ToList()
      : [];
    _board.SetJoinSlots(joinSlots.Select(pair => (pair.Cell, pair.Check.Cost)));
    // Теории, куда карточку можно положить взакрытую по праву Спецагента, тоже подсвечиваются.
    available.AddRange(_board.Field.HiddenPlacementTargets(source.Card, _session.CurrentPlayer)
      .Where(target => target.Check.IsAllowed && available.All(pair => pair.Cell != target.Column.Top.Above))
      .Select(target => (target.Column.Top.Above, target.Check)));
    _board.ShowAvailable(available);
    _personalHq.ShowAvailable(available.Where(pair => pair.Check.IsAllowed).Select(pair => pair.Cell));
    _board.ShowArrows(_board.Field.ArrowTargets(source.Card, _session.CurrentPlayer));
  }

  private void StartDrag(Drag drag)
  {
    drag.View.Position = MouseOnTable();
    // Карточку несут прямо, даже если на радиальном поле она лежала повёрнутой; на место её довернёт стол.
    drag.View.BaseAngle = 0;
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

  private void DropFromHand(HandDrag drag) => RunPlacement(drag, () => PlaceFromHand(drag));

  /// <summary>
  /// Выложить карточку из руки; если не получилось, вернуть её в руку и сказать почему.
  /// </summary>
  private void RunPlacement(HandDrag drag, Action place)
  {
    try
    {
      place();
    }
    // Любая ошибка выкладывания, а не только запрет правил: иначе карточка зависла бы над столом, а в руке — невидимой.
    catch (Exception error)
    {
      GD.PrintErr($"Не удалось выложить «{drag.View.Card}»: {error}");
      PlacementDenied?.Invoke(error.Message);
      // Если карточка на поле не легла, она возвращается в руку, а не остаётся висеть над столом.
      if (_board.Field.CellOf(drag.View.Card) is null)
        ReturnToHand(drag);
    }
    finally
    {
      SettleDrag(drag);
      // Карточка легла на поле — прятать в руке больше нечего (при возврате в руку её покажет ReturnToHand).
      if (_board.Field.CellOf(drag.View.Card) is not null && _hand.DraggedCard == drag.View.Card)
        _hand.DraggedCard = null;
    }
  }

  /// <summary>
  /// Карточка легла на поле, но её вид всё ещё в слое перетаскивания (например, из-за ошибки): переносим его в ячейку.
  /// </summary>
  private void SettleDrag(HandDrag drag)
  {
    var view = drag.View;
    if (view.GetParent() != _dragLayer || _board.Field.CellOf(view.Card) is not { } cell)
      return;

    // Карточка в теории отмечается миплом владельца; первая карточка новой теории — штаб.
    if (_board.Field.ColumnAt(cell) is { } column)
    {
      view.CardOwner = column.OwnerAt(cell);
      view.IsHeadquarters = column.IsHeadquarters && column.TheoryCell == cell;
    }

    _dragLayer.RemoveChild(view);
    _board.Place(view, cell.ToVector2I());
  }

  private void PlaceFromHand(HandDrag drag)
  {
    if (TargetCell(drag.View) is not { } cell)
    {
      ReturnToHand(drag);
      return;
    }

    var check = Check(drag.View, cell);
    var player = _session.CurrentPlayer;
    // Теория, куда игрок может выкладывать взакрытую: спрашиваем, как класть.
    if (_board.Field.HiddenPlacementColumnAt(cell.ToCell(), player) is { } hiddenColumn
      && _board.Field.CheckHiddenFromHand(drag.View.Card, hiddenColumn, player) is { IsAllowed: true } hiddenCheck)
    {
      var openCell = check.IsAllowed ? cell : (Vector2I?)null;
      ShowPlacementMenu(new HiddenChoice(drag, openCell, hiddenColumn), check, hiddenCheck);
      return;
    }

    if (!check.IsAllowed)
    {
      PlacementDenied?.Invoke(check.Reason!);
      ReturnToHand(drag);
      return;
    }

    PlaceOpen(drag, cell);
  }

  /// <summary>
  /// Положить карточку из руки лицом вверх в ячейку (правила уже проверены).
  /// </summary>
  private void PlaceOpen(HandDrag drag, Vector2I cell)
  {
    // Соседнюю карточку семейства теорий не занимают: над ней карточка вставляется, а она и её сторона сдвигаются.
    if (_board.Field.NeighbourBoundary(drag.View.Card, cell.ToCell()) is { } boundary)
    {
      DropInWall(drag, boundary);
      return;
    }

    if (Net is { } net)
    {
      SendPlacement(net, drag, new PlaceCommand(_session.CardIds.IdOf(drag.View.Card), cell.ToCell()));
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
  /// Спросить, как положить карточку в теорию, куда игрок может выкладывать взакрытую. Пока меню открыто,
  /// карточка висит над столом.
  /// </summary>
  private void ShowPlacementMenu(HiddenChoice choice, PlacementCheck open, PlacementCheck hidden)
  {
    _hiddenChoice = choice;
    _placementMenu.Clear();
    _placementMenu.AddSeparator($"{choice.Drag.View.Card.Name} → «{choice.Column.Theory}»");
    if (choice.OpenCell is not null)
      _placementMenu.AddItem($"Выложить открыто{CostSuffix(open.Cost)}", OpenPlacementId);
    _placementMenu.AddItem($"Выложить взакрытую{CostSuffix(hidden.Cost)}", HiddenPlacementId);
    _placementMenu.ResetSize();
    _placementMenu.Popup(new Rect2I((Vector2I)MouseOnScreen(), Vector2I.Zero));
  }

  private static string CostSuffix(int cost) => cost > 0 ? $" ({cost}$)" : " (бесплатно)";

  private void OnPlacementMenuIdPressed(long id)
  {
    if (_hiddenChoice is not { } choice)
      return;
    _hiddenChoice = null;
    if (id == OpenPlacementId && choice.OpenCell is { } cell)
      RunPlacement(choice.Drag, () => PlaceOpen(choice.Drag, cell));
    else if (id == HiddenPlacementId)
      RunPlacement(choice.Drag, () => PlaceHidden(choice.Drag, choice.Column));
    else
      ReturnToHand(choice.Drag);
  }

  private void CancelHiddenChoice()
  {
    if (_hiddenChoice is not { } choice)
      return;
    _hiddenChoice = null;
    ReturnToHand(choice.Drag);
  }

  /// <summary>
  /// Положить карточку из руки взакрытую сверху стопки теории. Закрытую карточку на столе рисует сам стол
  /// (<see cref="PlayingField.HiddenCardPlaced"/>), поэтому летящая карточка больше не нужна.
  /// </summary>
  private void PlaceHidden(HandDrag drag, TheoryColumn column)
  {
    if (Net is { } net)
    {
      // Закрытую карточку на столе нарисует стол по новой партии: летящая карточка не нужна.
      net.Send(new PlaceHiddenCommand(_session.CardIds.IdOf(drag.View.Card), column.Theory.Name), accepted =>
      {
        if (!accepted)
        {
          ReturnToHand(drag);
          return;
        }
        drag.View.QueueFree();
        if (_hand.DraggedCard == drag.View.Card)
          _hand.DraggedCard = null;
      });
      return;
    }

    _session.PlaceHiddenFromHand(drag.View.Card, column);
    drag.View.QueueFree();
    if (_hand.DraggedCard == drag.View.Card)
      _hand.DraggedCard = null;
  }

  /// <summary>
  /// Дочерняя теория вставлена в перегородку: теории слева сдвигаются, карточка ложится в освободившийся столбец.
  /// </summary>
  private void DropInWall(HandDrag drag, int boundary)
  {
    if (Net is { } net)
    {
      SendPlacement(net, drag, new PlaceInWallCommand(_session.CardIds.IdOf(drag.View.Card), boundary));
      return;
    }

    var player = _session.CurrentPlayer;
    var placed = _session.PlaceInWallFromHand(drag.View.Card, boundary);
    var cell = _board.Field.CellOf(drag.View.Card)!.Value.ToVector2I();
    drag.View.IsHeadquarters = placed.CreatesHeadquarters;
    drag.View.CardOwner = player;
    _dragLayer.RemoveChild(drag.View);
    _board.Place(drag.View, cell);
  }

  /// <summary>
  /// Бот выкладывает карточку с руки: она вылетает из его личного штаба и ложится в ячейку, как у игрока.
  /// </summary>
  /// <returns>Условия, на которых карточка положена.</returns>
  public PlacementCheck PlaceForBot(Card card, Cell cell)
  {
    var player = _session.CurrentPlayer;
    var view = CardView.Create(card);
    view.Position = _board.ZoneCenter(_board.Field.PlayerZones[player]);
    PlacementCheck placed;
    try
    {
      placed = _session.PlaceFromHand(card, cell);
    }
    catch
    {
      view.QueueFree();
      throw;
    }

    // Действие при выкладывании могло сдвинуть карточку.
    var actual = (_board.Field.CellOf(card) ?? cell).ToVector2I();
    view.IsHeadquarters = placed.CreatesHeadquarters;
    if (_board.Field.CommonZone.Contains(actual.ToCell()))
      view.CardOwner = player;
    _board.Place(view, actual);
    return placed;
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

    if (Net is { } net)
    {
      // Купленную карточку покажет рука по новой партии; не купили — карточка возвращается на рынок.
      net.Send(new BuyCommand(drag.Cell.ToCell()), accepted =>
      {
        if (accepted)
          drag.View.QueueFree();
        else
          ReturnToMarket(drag);
      });
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
    _personalHq.ShowAvailable([]);
    _personalHq.Highlight(null, false);
    _hand.DropState = HandDropState.None;
    _market.DraggedCell = null;
    Input.SetDefaultCursorShape();
    return drag;
  }

  private void ReturnToHand(HandDrag drag)
  {
    var view = drag.View;
    var source = drag.Source;
    var card = view.Card;
    // Рука живёт в координатах экрана, карточка — в координатах стола. Окно руки могли закрыть — тогда карточка
    // просто исчезает на месте.
    var target = IsInstanceValid(source) && source.IsVisibleInTree()
      ? GetViewport().GetCanvasTransform().AffineInverse() * source.Center
      : view.Position;
    var tween = view.CreateTween();
    tween.TweenProperty(view, Node2D.PropertyName.Position.ToString(), target, ReturnDuration)
      .SetTrans(Tween.TransitionType.Cubic)
      .SetEase(Tween.EaseType.Out);
    tween.TweenCallback(Callable.From(() =>
    {
      view.QueueFree();
      if (_hand.DraggedCard == card)
        _hand.DraggedCard = null;
    }));
  }

  private void ReturnToMarket(MarketDrag drag)
  {
    _dragLayer.RemoveChild(drag.View);
    _board.ReturnMarketView(drag.View, drag.Cell);
  }

  /// <summary>
  /// Куда ляжет карточка из руки: в ячейку своего штаба в окне, если карточка над ним, иначе — в ячейку стола под ней.
  /// Над окном штаба (своего, но не над ячейкой, или чужого) карточку положить некуда.
  /// </summary>
  private Vector2I? TargetCell(CardView view)
  {
    var screen = MouseOnScreen();
    if (_personalHq.ContainsScreenPoint(screen))
      return _personalHq.CellAtScreenPoint(screen)?.ToVector2I();
    return _board.CellAt(view.Position);
  }

  private PlacementCheck Check(CardView view, Vector2I cell) => _board.Field.CheckDrop(view.Card, cell.ToCell(), _session.CurrentPlayer);

  /// <summary>
  /// Подсветка под летящей карточкой: можно положить открыто или взакрытую (по праву Спецагента).
  /// </summary>
  private PlacementCheck DropCheck(CardView view, Vector2I cell)
  {
    var check = Check(view, cell);
    var player = _session.CurrentPlayer;
    if (check.IsAllowed || _board.Field.HiddenPlacementColumnAt(cell.ToCell(), player) is not { } column)
      return check;
    var hidden = _board.Field.CheckHiddenFromHand(view.Card, column, player);
    return hidden.IsAllowed ? hidden : check;
  }

  /// <summary>
  /// Брать карточки можно только в фазе ходов игроков, когда ничего не ждёт выбора: ни проверка успеха, ни цель
  /// выложенной карточки, ни действие основной теории, ни выбор цели карточки (<see cref="IsChoosing"/>).
  /// </summary>
  private bool CanAct =>
    _session.Phase == GamePhase.PlayerTurns
    && !_session.CurrentPlayer.IsBot
    && _session.PendingCheck is null
    && _session.PendingPlacement is null
    && _session.PendingTheoryTrigger is null
    && _hiddenChoice is null
    && !(IsChoosing?.Invoke() ?? false);

  /// <summary>
  /// Игрок сейчас выбирает действие карточки или его цель (см. <see cref="CardUseController.IsChoosing"/>).
  /// </summary>
  public Func<bool>? IsChoosing { get; set; }

  private Vector2 MouseOnTable() => _board.GetGlobalMousePosition();

  private Vector2 MouseOnScreen() => GetViewport().GetMousePosition();
}
