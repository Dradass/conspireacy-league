using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Game.Ui;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Правая кнопка мыши по карточке открывает окно с её описанием: на столе (персонаж, карточка рынка, карточка теории)
/// и в руке.
/// </summary>
public partial class CardInfoController : Node
{
  private Board _board = null!;
  private GameSession _session = null!;
  private CardInfoPanel _panel = null!;
  private HandHub _hand = null!;
  private MarketHub _market = null!;
  private TheoriesHub _theories = null!;

  /// <summary>
  /// Сколько секунд нужно удерживать правую кнопку, чтобы открылось описание.
  /// </summary>
  private const double HoldSeconds = 0.5;

  /// <summary>
  /// На сколько пикселей курсор может сдвинуться за время удержания; дальше это перетаскивание, а не удержание.
  /// </summary>
  private const float MoveTolerance = 5f;

  /// <summary>
  /// Что показать, когда кнопку удержат достаточно долго; <c>null</c>, если удержания нет.
  /// </summary>
  private Action? _pendingInfo;
  private double _heldFor;
  private Vector2 _pressPoint;

  /// <summary>
  /// Настроить контроллер.
  /// </summary>
  /// <param name="board">Стол.</param>
  /// <param name="session">Сессия игры.</param>
  /// <param name="panel">Окно информации.</param>
  /// <param name="hand">Окна руки игрока, который ходит.</param>
  /// <param name="market">Окна рынка.</param>
  /// <param name="theories">Окна «Теории»: в них, как и на столе, можно посмотреть описание карточки.</param>
  public void Initialize(Board board, GameSession session, CardInfoPanel panel, HandHub hand, MarketHub market, TheoriesHub theories)
  {
    _theories = theories;
    _theories.InfoPressed += OnTablePointPressed;
    _theories.InfoReleased += CancelHold;
    _market = market;
    _market.CardInfoPressed += OnHandCardPressed;
    _market.CardInfoReleased += CancelHold;
    _board = board;
    _session = session;
    _panel = panel;
    _hand = hand;
    _hand.CardInfoPressed += OnHandCardPressed;
    _hand.CardInfoReleased += CancelHold;
  }

  public override void _ExitTree()
  {
    _hand.CardInfoPressed -= OnHandCardPressed;
    _hand.CardInfoReleased -= CancelHold;
    _market.CardInfoPressed -= OnHandCardPressed;
    _market.CardInfoReleased -= CancelHold;
    _theories.InfoPressed -= OnTablePointPressed;
    _theories.InfoReleased -= CancelHold;
  }

  public override void _Process(double delta)
  {
    if (_pendingInfo is null)
      return;

    // Курсор уехал дальше допуска: это не удержание, описание не открываем.
    if (GetViewport().GetMousePosition().DistanceTo(_pressPoint) > MoveTolerance)
    {
      CancelHold();
      return;
    }

    _heldFor += delta;
    if (_heldFor < HoldSeconds)
      return;

    var show = _pendingInfo;
    _pendingInfo = null;
    show();
  }

  public override void _UnhandledInput(InputEvent @event)
  {
    if (@event is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Right })
    {
      CancelHold();
      return;
    }
    if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
      return;
    if (InfoAt(_board.GetGlobalMousePosition()) is not { } show)
      return;

    GetViewport().SetInputAsHandled();
    StartHold(show);
  }

  /// <summary>
  /// Правой кнопкой нажали в окне «Теории» над точкой стола.
  /// </summary>
  private void OnTablePointPressed(Vector2 point)
  {
    if (InfoAt(point) is { } show)
      StartHold(show);
  }

  /// <summary>
  /// Что показать по карточке в точке стола (в мировых координатах); <c>null</c> — там нет карточки.
  /// </summary>
  private Action? InfoAt(Vector2 point)
  {
    if (_board.CellAt(point) is not { } cell)
      return null;

    Action? show = null;
    if (_board.TheoryCardAt(cell) is { } owned)
      show = () => _panel.Open(CardInfoText.TheoryText(owned.Card.Theory));
    else if (_board.MarketViewAt(cell) is { } market)
      show = () => ShowCard(market.Card, null);
    else if (_board.ViewAt(cell) is { } view)
      show = () => ShowCard(view.Card, cell.ToCell());
    return show;
  }

  private void OnHandCardPressed(Card card) => StartHold(() => ShowCard(card, null));

  /// <summary>
  /// Начать отсчёт удержания правой кнопки: через <see cref="HoldSeconds"/> откроется описание.
  /// </summary>
  private void StartHold(Action show)
  {
    _pendingInfo = show;
    _heldFor = 0;
    _pressPoint = GetViewport().GetMousePosition();
  }

  /// <summary>
  /// Правую кнопку отпустили раньше времени: описание не открываем.
  /// </summary>
  private void CancelHold() => _pendingInfo = null;

  /// <summary>
  /// Открыть описание карточки. <paramref name="cell"/> — где карточка лежит на столе, <c>null</c> — в руке или на рынке.
  /// </summary>
  private void ShowCard(Card card, Cell? cell)
  {
    switch (card)
    {
      case HiddenCard hidden when !CanSee(hidden, cell):
        _panel.Open(CardInfoText.Hidden);
        break;
      case HiddenCard hidden when hidden.Face is PersonCard face:
        _panel.Open(CardInfoText.PersonText(face, ChanceOf(hidden, cell)));
        break;
      case PersonCard person:
        _panel.Open(CardInfoText.PersonText(person, ChanceOf(person, cell)));
        break;
      case TheoryCard theory:
        _panel.Open(CardInfoText.TheoryText(theory.Theory));
        break;
      default:
        _panel.Open(card.Name);
        break;
    }
  }

  /// <summary>
  /// Закрытую карту видит тот, кто её открыл или подсмотрел, владелец в своей рабочей зоне и все, кому это дано.
  /// </summary>
  private bool CanSee(HiddenCard hidden, Cell? cell)
  {
    var viewer = _board.Viewer;
    var inOwnZone = cell is { } c && _session.Field.PlayerZones.TryGetValue(viewer, out var zone) && zone.Contains(c);
    return hidden.IsRevealed || hidden.IsKnownBy(viewer) || inOwnZone || _session.Field.CanSeeAllHidden(viewer);
  }

  /// <summary>
  /// Текущая вероятность успеха на столе (с бонусами и штрафами); в руке и на рынке — базовая.
  /// </summary>
  private int ChanceOf(PersonCard person, Cell? cell) =>
    cell is { } c ? _session.Field.SuccessChanceAt(c) : person.SuccessChancePercent;
}
