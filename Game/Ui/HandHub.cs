using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Game;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Общее состояние всех окон «Рука» (<see cref="HandView"/>): чья рука показана, идёт ли выбор карточки, какая карточка
/// выбрана или перетаскивается и как подсвечена рука. Контроллеры работают с ним, а не с конкретным окном, —
/// поэтому окон с рукой может быть сколько угодно (или ни одного).
/// </summary>
public sealed class HandHub
{
  private readonly List<HandView> _views = new();
  private Hand _hand;
  private bool _picking;
  private Card? _selectedCard;
  private Card? _draggedCard;
  private HandDropState _dropState;

  /// <param name="session">Сессия игры: показывается рука игрока, который сейчас ходит. Руку бота не показываем —
  /// пока ходит бот, видна рука последнего игрока-человека.</param>
  public HandHub(GameSession session)
  {
    _hand = (session.Players.FirstOrDefault(player => !player.IsBot) ?? session.CurrentPlayer).Hand;
    _hand.Changed += OnHandChanged;
    session.Turns.TurnChanged += ShowHandOf;
  }

  /// <summary>
  /// Показываемая рука.
  /// </summary>
  public Hand Hand => _hand;

  /// <summary>
  /// Сменилась рука или её карточки.
  /// </summary>
  public event Action? HandChanged;

  /// <summary>
  /// Сменилась выбранная или перетаскиваемая карточка.
  /// </summary>
  public event Action? SelectionChanged;

  /// <summary>
  /// Сменилась подсветка руки (вызывается и без изменения значения: окно под курсором могло смениться).
  /// </summary>
  public event Action? DropStateChanged;

  /// <summary>
  /// Игрок начал тянуть карточку из руки.
  /// </summary>
  public event Action<HandCardView>? CardDragStarted;

  /// <summary>
  /// Правой кнопкой нажата карточка в руке; аргумент — карточка.
  /// </summary>
  public event Action<Card>? CardInfoPressed;

  /// <summary>
  /// Правую кнопку отпустили.
  /// </summary>
  public event Action? CardInfoReleased;

  /// <summary>
  /// Игрок выбрал карточку в руке, пока идёт выбор (<see cref="Picking"/>).
  /// </summary>
  public event Action<Card>? CardPicked;

  /// <summary>
  /// Игрок выбирает карточку в руке для действия: щелчок по карточке выбирает её, а не начинает перетаскивание.
  /// Рука подсвечена.
  /// </summary>
  public bool Picking
  {
    get => _picking;
    set
    {
      _picking = value;
      DropState = value ? HandDropState.Target : HandDropState.None;
    }
  }

  /// <summary>
  /// Выбранная карточка: она выделена рамкой. <c>null</c> — ничего не выбрано.
  /// </summary>
  public Card? SelectedCard
  {
    get => _selectedCard;
    set
    {
      _selectedCard = value;
      SelectionChanged?.Invoke();
    }
  }

  /// <summary>
  /// Карточка, которую сейчас тащат из руки: во всех окнах руки она скрыта.
  /// </summary>
  public Card? DraggedCard
  {
    get => _draggedCard;
    set
    {
      _draggedCard = value;
      SelectionChanged?.Invoke();
    }
  }

  /// <summary>
  /// Подсветка руки как места, куда можно положить карточку. <see cref="HandDropState.Hover"/> подсвечивает
  /// только окно под курсором.
  /// </summary>
  public HandDropState DropState
  {
    get => _dropState;
    set
    {
      _dropState = value;
      DropStateChanged?.Invoke();
    }
  }

  /// <summary>
  /// Показать руку указанного игрока (например, владельца основной теории, который выбирает карточку в руке).
  /// </summary>
  public void ShowHandOf(Player player)
  {
    // Рука бота — секрет для людей за столом.
    if (player.IsBot)
      return;
    _hand.Changed -= OnHandChanged;
    _hand = player.Hand;
    _hand.Changed += OnHandChanged;
    OnHandChanged();
  }

  /// <summary>
  /// Находится ли точка экрана над каким-нибудь видимым окном руки.
  /// </summary>
  public bool ContainsScreenPoint(Vector2 point) => _views.Any(view => view.ContainsScreenPoint(point));

  internal void Register(HandView view) => _views.Add(view);

  internal void Unregister(HandView view) => _views.Remove(view);

  internal void RaiseDragStarted(HandCardView view) => CardDragStarted?.Invoke(view);

  internal void RaiseInfoPressed(Card card) => CardInfoPressed?.Invoke(card);

  internal void RaiseInfoReleased() => CardInfoReleased?.Invoke();

  internal void RaisePicked(Card card) => CardPicked?.Invoke(card);

  private void OnHandChanged() => HandChanged?.Invoke();
}
