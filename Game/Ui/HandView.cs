using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Окно руки: карточки руки, показанной в <see cref="HandHub"/> (обычно — игрока, который сейчас ходит),
/// в слабо заметной серой области. Карточки отсюда выкладывают на стол, а с рынка покупают сюда.
/// Всё состояние (выбор, подсветка, перетаскиваемая карточка) общее для всех окон руки и хранится в хабе.
/// </summary>
public partial class HandView : PanelContainer
{
  private const int MinVisibleCards = 5;
  private const int Separation = 8;

  // В стиле интерфейса (UiStyle): тёмно-зелёная подложка и зелёная рамка; при наведении карточки — ярче.
  private static readonly Color FillColor = new(0.05f, 0.1f, 0.07f, 0.55f);
  private static readonly Color BorderColor = UiStyle.Glow with { A = 0.35f };
  private static readonly Color TargetBorderColor = UiStyle.Glow with { A = 0.7f };
  private static readonly Color HoverFillColor = new(0.55f, 0.95f, 0.6f, 0.2f);
  private static readonly Color HoverBorderColor = new(0.55f, 0.95f, 0.6f, 0.8f);
  private static readonly Color EmptyColor = new(1, 1, 1, 0.35f);

  private readonly StyleBoxFlat _style = new();
  private HBoxContainer _cards = null!;
  private Label _empty = null!;

  /// <summary>
  /// Общее состояние окон руки.
  /// </summary>
  public HandHub Hub { get; init; } = null!;

  public override void _Ready()
  {
    _style.SetBorderWidthAll(1);
    _style.SetCornerRadiusAll(3);
    _style.SetContentMarginAll(10);
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

    Hub.Register(this);
    Hub.HandChanged += Refresh;
    Hub.SelectionChanged += ApplySelection;
    Hub.DropStateChanged += ApplyDropState;
    Refresh();
    ApplyDropState();
  }

  public override void _ExitTree()
  {
    Hub.Unregister(this);
    Hub.HandChanged -= Refresh;
    Hub.SelectionChanged -= ApplySelection;
    Hub.DropStateChanged -= ApplyDropState;
  }

  /// <summary>
  /// Находится ли точка экрана над этим окном руки (над всем окном, а не только над карточками). Скрытое окно
  /// (раскладка другого игрока) карточку не принимает.
  /// </summary>
  public bool ContainsScreenPoint(Vector2 point) =>
    IsVisibleInTree() && (HudWindow.Of(this) ?? (Control)this).GetGlobalRect().HasPoint(point);

  private void Refresh()
  {
    // Убираем старые карточки сразу, а не в конце кадра: иначе они ещё учитываются в размере руки,
    // по которому окно подбирает размер и масштаб.
    foreach (var child in _cards.GetChildren())
    {
      _cards.RemoveChild(child);
      child.QueueFree();
    }

    foreach (var card in Hub.Hand.Cards)
    {
      var view = HandCardView.Create(card);
      view.DragStarted += OnCardPressed;
      view.InfoPressed += source => Hub.RaiseInfoPressed(source.Card);
      view.InfoReleased += _ => Hub.RaiseInfoReleased();
      _cards.AddChild(view);
    }

    ApplySelection();
    _empty.Visible = Hub.Hand.Cards.Count == 0;
  }

  private void ApplySelection()
  {
    foreach (var view in _cards.GetChildren().OfType<HandCardView>())
    {
      view.Selected = view.Card == Hub.SelectedCard;
      // Карточка остаётся в руке, пока её не выложат; на время перетаскивания прячем её.
      view.Modulate = view.Card == Hub.DraggedCard ? Colors.Transparent : Colors.White;
    }
  }

  private void ApplyDropState()
  {
    var state = Hub.DropState;
    if (state == HandDropState.Hover && !ContainsScreenPoint(GetViewport().GetMousePosition()))
      state = HandDropState.Target;

    _style.BgColor = state == HandDropState.Hover ? HoverFillColor : FillColor;
    _style.BorderColor = state switch
    {
      HandDropState.Hover => HoverBorderColor,
      HandDropState.Target => TargetBorderColor,
      _ => BorderColor,
    };
  }

  private void OnCardPressed(HandCardView source)
  {
    if (Hub.Picking)
      Hub.RaisePicked(source.Card);
    else
      Hub.RaiseDragStarted(source);
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
