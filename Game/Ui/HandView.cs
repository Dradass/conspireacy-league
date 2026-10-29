using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Game;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Рука игрока, который сейчас ходит: его карточки в слабо заметной серой области.
/// Карточки отсюда выкладывают на стол, а с рынка покупают сюда.
/// </summary>
public partial class HandView : PanelContainer
{
  private const int MinVisibleCards = 5;
  private const int Separation = 8;

  private static readonly Color FillColor = new(0.6f, 0.6f, 0.6f, 0.12f);
  private static readonly Color BorderColor = new(0.7f, 0.7f, 0.7f, 0.25f);
  private static readonly Color TargetBorderColor = new(0.85f, 0.85f, 0.85f, 0.6f);
  private static readonly Color HoverFillColor = new(0.55f, 0.95f, 0.6f, 0.2f);
  private static readonly Color HoverBorderColor = new(0.55f, 0.95f, 0.6f, 0.8f);
  private static readonly Color EmptyColor = new(1, 1, 1, 0.35f);

  private readonly StyleBoxFlat _style = new();
  private HBoxContainer _cards = null!;
  private Label _empty = null!;
  private Hand? _hand;
  private HandDropState _dropState;
  private bool _picking;
  private Card? _selectedCard;

  /// <summary>
  /// Сессия игры: показывается рука игрока, который сейчас ходит.
  /// </summary>
  public GameSession Session { get; init; } = null!;

  /// <summary>
  /// Игрок начал тянуть карточку из руки.
  /// </summary>
  public event Action<HandCardView>? CardDragStarted;

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
      foreach (var view in _cards.GetChildren().OfType<HandCardView>())
        view.Selected = view.Card == value;
    }
  }

  /// <summary>
  /// Подсветка руки как места, куда можно положить карточку.
  /// </summary>
  public HandDropState DropState
  {
    get => _dropState;
    set
    {
      _dropState = value;
      _style.BgColor = value == HandDropState.Hover ? HoverFillColor : FillColor;
      _style.BorderColor = value switch
      {
        HandDropState.Hover => HoverBorderColor,
        HandDropState.Target => TargetBorderColor,
        _ => BorderColor,
      };
    }
  }

  public override void _Ready()
  {
    _style.SetBorderWidthAll(2);
    _style.SetCornerRadiusAll(8);
    _style.SetContentMarginAll(10);
    DropState = HandDropState.None;
    AddThemeStyleboxOverride("panel", _style);
    MouseFilter = MouseFilterEnum.Ignore;

    _cards = new HBoxContainer
    {
      Name = "Cards",
      MouseFilter = MouseFilterEnum.Ignore,
      CustomMinimumSize = new Vector2(
        HandCardView.CardSize.X * MinVisibleCards + Separation * (MinVisibleCards - 1),
        HandCardView.CardSize.Y),
    };
    _cards.AddThemeConstantOverride("separation", Separation);
    AddChild(_cards);

    _empty = new Label
    {
      Text = "Рука пуста",
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Center,
      MouseFilter = MouseFilterEnum.Ignore,
    };
    _empty.AddThemeFontSizeOverride("font_size", 16);
    _empty.AddThemeColorOverride("font_color", EmptyColor);
    AddChild(_empty);

    Session.Turns.TurnChanged += ShowHandOf;
    ShowHandOf(Session.CurrentPlayer);
  }

  public override void _ExitTree()
  {
    Session.Turns.TurnChanged -= ShowHandOf;
    if (_hand is not null)
      _hand.Changed -= Refresh;
  }

  /// <summary>
  /// Находится ли точка экрана над рукой.
  /// </summary>
  public bool ContainsScreenPoint(Vector2 point) => GetGlobalRect().HasPoint(point);

  private void ShowHandOf(Player player)
  {
    if (_hand is not null)
      _hand.Changed -= Refresh;
    _hand = player.Hand;
    _hand.Changed += Refresh;
    Refresh();
  }

  private void Refresh()
  {
    foreach (var child in _cards.GetChildren())
      child.QueueFree();

    foreach (var card in _hand!.Cards)
    {
      var view = HandCardView.Create(card);
      view.Selected = card == _selectedCard;
      view.DragStarted += OnCardPressed;
      _cards.AddChild(view);
    }

    _empty.Visible = _hand.Cards.Count == 0;
  }

  private void OnCardPressed(HandCardView source)
  {
    if (_picking)
      CardPicked?.Invoke(source.Card);
    else
      CardDragStarted?.Invoke(source);
  }
}

/// <summary>
/// Подсветка руки при перетаскивании карточки.
/// </summary>
public enum HandDropState
{
  /// <summary>
  /// Без подсветки.
  /// </summary>
  None,

  /// <summary>
  /// Карточку можно положить в руку.
  /// </summary>
  Target,

  /// <summary>
  /// Карточка над рукой: отпустите — и она окажется в руке.
  /// </summary>
  Hover,
}
