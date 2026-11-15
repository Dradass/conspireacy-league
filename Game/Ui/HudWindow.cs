using ConspiracyLeague.Core.Game;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Что показывает окно интерфейса (<see cref="HudWindow"/>).
/// </summary>
public enum HudWindowKind
{
  /// <summary>
  /// Рынок: колода и открытые карточки.
  /// </summary>
  Market,

  /// <summary>
  /// Все выложенные штабы теорий — как на столе.
  /// </summary>
  Theories,

  /// <summary>
  /// Рука игрока, который ходит.
  /// </summary>
  Hand,

  /// <summary>
  /// Личный штаб игрока, который ходит.
  /// </summary>
  PersonalHq,

  /// <summary>
  /// Личный штаб выбранного другого игрока.
  /// </summary>
  OtherHq,

  /// <summary>
  /// Раунд, ход, игроки и их суммы, кнопка «Пас».
  /// </summary>
  Players,

  /// <summary>
  /// История ходов всех игроков.
  /// </summary>
  History,
}

/// <summary>
/// Содержимое окна, которое не масштабируется под окно, а занимает его в натуральную величину (например, список
/// с прокруткой: в растянутом окне видно больше строк). Его минимальный размер — предел сжатия окна, а размер
/// при открытии окна — <see cref="InitialSize"/>.
/// </summary>
public interface IFillsHudWindow
{
  /// <summary>
  /// Размер содержимого, когда окно только открыли.
  /// </summary>
  Vector2 InitialSize { get; }
}

/// <summary>
/// Окно интерфейса, которое игрок настраивает сам: его можно перетаскивать за заголовок и растягивать за края,
/// а содержимое масштабируется под размер окна. В заголовке — выбор того, что показывает окно (<see cref="HudWindowKind"/>),
/// для штаба другого игрока — переключатель «влево-вправо», кнопка с канцелярской кнопкой (закрепить: окно больше
/// не двигается и не растягивается; нажать ещё раз — открепить) и крестик, закрывающий окно.
/// </summary>
public partial class HudWindow : PanelContainer
{
  private const int PanelMargin = 7;
  private const float ResizeMargin = 7f;
  private const float HeaderButtonSize = 26f;
  private const float GripMinWidth = 24f;
  private const int HeaderSeparation = 4;
  private const int HeaderFontSize = 13;
  private const float PlayerNameWidth = 96f;
  private const float KindPickerMinWidth = 96f;
  private const float KindPickerStretch = 2f;
  private const float MinHeight = 80f;
  private const float MinContentScale = 0.2f;
  private const float MaxContentScale = 2.5f;

  /// <summary>
  /// Сколько пикселей окна должно оставаться на экране, когда его утаскивают за край.
  /// </summary>
  private const float KeepVisible = 48f;

  private static readonly (HudWindowKind Kind, string Title)[] Kinds =
  [
    (HudWindowKind.Market, "Рынок"),
    (HudWindowKind.Theories, "Теории"),
    (HudWindowKind.Hand, "Рука"),
    (HudWindowKind.PersonalHq, "Личный штаб"),
    (HudWindowKind.OtherHq, "Штаб другого игрока"),
    (HudWindowKind.Players, "Информация об игроках"),
    (HudWindowKind.History, "История"),
  ];

  private static readonly Color IconColor = UiStyle.Text with { A = 0.85f };
  private static readonly Color GripColor = UiStyle.Glow with { A = 0.35f };

  [Flags]
  private enum Edge
  {
    None = 0,
    Left = 1,
    Right = 2,
    Top = 4,
    Bottom = 8,
  }

  private HBoxContainer _header = null!;
  private OptionButton _kindPicker = null!;
  private HBoxContainer _playerPicker = null!;
  private Label _playerName = null!;
  private Control _grip = null!;
  private Button _pin = null!;
  private Control _host = null!;
  private Control? _content;
  private bool _pinned;
  private Func<Vector2, Vector2, Vector2>? _placement;

  private Edge _resizing;
  private bool _moving;
  private Vector2 _pressMouse;
  private Rect2 _pressRect;

  /// <summary>
  /// Игрок, в чьей раскладке окно. «Штаб другого игрока» показывает кого угодно, кроме него.
  /// </summary>
  public Player LayoutOwner { get; init; } = null!;

  /// <summary>
  /// Все игроки партии по порядку: среди них переключается «Штаб другого игрока».
  /// </summary>
  public IReadOnlyList<Player> Players { get; init; } = [];

  /// <summary>
  /// Создаёт содержимое окна по его типу (<see cref="Kind"/>) и, для штаба другого игрока, выбранному игроку (<see cref="ShownPlayer"/>).
  /// </summary>
  public Func<HudWindow, Control> ContentFactory { get; init; } = null!;

  /// <summary>
  /// Что показывает окно.
  /// </summary>
  public HudWindowKind Kind { get; private set; }

  /// <summary>
  /// С каким содержимым окно откроется.
  /// </summary>
  public HudWindowKind InitialKind
  {
    init => Kind = value;
  }

  /// <summary>
  /// Чей штаб показывает окно «Штаб другого игрока».
  /// </summary>
  public Player? ShownPlayer { get; private set; }

  /// <summary>
  /// Где поставить окно, когда станет известен его размер: (размер окна, размер экрана) → левый верхний угол.
  /// По умолчанию — по центру экрана.
  /// </summary>
  public Func<Vector2, Vector2, Vector2>? Placement
  {
    init => _placement = value;
  }

  /// <summary>
  /// Окно закреплено: его нельзя перемещать и растягивать.
  /// </summary>
  public bool Pinned
  {
    get => _pinned;
    set
    {
      _pinned = value;
      if (_pin is null)
        return;
      _pin.SetPressedNoSignal(value);
      UpdatePinState();
    }
  }

  /// <summary>
  /// Окно, в котором лежит узел, или <c>null</c>, если он не в окне.
  /// </summary>
  public static HudWindow? Of(Node node)
  {
    for (var parent = node.GetParent(); parent is not null; parent = parent.GetParent())
    {
      if (parent is HudWindow window)
        return window;
    }

    return null;
  }

  public override void _Ready()
  {
    MouseFilter = MouseFilterEnum.Stop;
    AddThemeStyleboxOverride("panel", UiStyle.Panel(margin: PanelMargin, alpha: 0.85f));

    var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
    column.AddThemeConstantOverride("separation", HeaderSeparation);
    AddChild(column);

    _header = new HBoxContainer { Name = "Header", MouseFilter = MouseFilterEnum.Ignore };
    _header.AddThemeConstantOverride("separation", HeaderSeparation);
    column.AddChild(_header);

    // Список сжимается (название обрезается), чтобы окно можно было сделать заметно уже своего начального размера.
    _kindPicker = new OptionButton
    {
      Name = "Kind",
      FocusMode = FocusModeEnum.None,
      TooltipText = "Что показывать в окне",
      FitToLongestItem = false,
      ClipText = true,
      CustomMinimumSize = new Vector2(KindPickerMinWidth, 0),
      // Свободное место заголовка делят список и поле для перетаскивания: списку — больше, чтобы название не обрезалось.
      SizeFlagsHorizontal = SizeFlags.ExpandFill,
      SizeFlagsStretchRatio = KindPickerStretch,
    };
    _kindPicker.AddThemeFontSizeOverride("font_size", HeaderFontSize);
    foreach (var (kind, title) in Kinds)
      _kindPicker.AddItem(title, (int)kind);
    _kindPicker.Select(_kindPicker.GetItemIndex((int)Kind));
    _kindPicker.ItemSelected += index => SetKind((HudWindowKind)_kindPicker.GetItemId((int)index));
    _header.AddChild(_kindPicker);

    _header.AddChild(CreatePlayerPicker());

    _grip = new Control
    {
      Name = "Grip",
      SizeFlagsHorizontal = SizeFlags.ExpandFill,
      CustomMinimumSize = new Vector2(GripMinWidth, HeaderButtonSize),
      MouseFilter = MouseFilterEnum.Stop,
    };
    _grip.Draw += DrawGrip;
    _grip.GuiInput += OnGripInput;
    _header.AddChild(_grip);

    _pin = CreateIconButton("Pin", DrawPin);
    _pin.ToggleMode = true;
    _pin.SetPressedNoSignal(_pinned);
    _pin.Toggled += pressed =>
    {
      _pinned = pressed;
      UpdatePinState();
    };
    _header.AddChild(_pin);

    var close = CreateIconButton("Close", DrawClose);
    close.TooltipText = "Закрыть окно";
    close.Pressed += QueueFree;
    _header.AddChild(close);

    _host = new Control
    {
      Name = "Content",
      MouseFilter = MouseFilterEnum.Ignore,
      SizeFlagsHorizontal = SizeFlags.ExpandFill,
      SizeFlagsVertical = SizeFlags.ExpandFill,
    };
    _host.Resized += LayoutContent;
    column.AddChild(_host);

    ShownPlayer = OtherPlayers().FirstOrDefault() ?? LayoutOwner;
    UpdatePinState();
    RebuildContent();
    // Размер содержимого становится известен после его _Ready и первой раскладки — подгоняем окно кадром позже.
    Callable.From(FitToContent).CallDeferred();
  }

  public override void _GuiInput(InputEvent @event)
  {
    switch (@event)
    {
      case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press:
        MoveToFront();
        _resizing = _pinned ? Edge.None : EdgeAt(press.Position);
        _pressMouse = press.GlobalPosition;
        _pressRect = new Rect2(Position, Size);
        AcceptEvent();
        break;
      case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
        _resizing = Edge.None;
        AcceptEvent();
        break;
      case InputEventMouseMotion motion:
        if (_resizing != Edge.None)
        {
          Resize(motion.GlobalPosition);
          AcceptEvent();
        }
        else
          MouseDefaultCursorShape = _pinned ? CursorShape.Arrow : CursorOf(EdgeAt(motion.Position));
        break;
    }
  }

  /// <summary>
  /// Вернуть окно на экран, если оно оказалось за краем (например, после изменения размера окна игры).
  /// </summary>
  public void ClampToViewport()
  {
    var screen = GetViewportRect().Size;
    Position = new Vector2(
      Mathf.Clamp(Position.X, KeepVisible - Size.X, screen.X - KeepVisible),
      Mathf.Clamp(Position.Y, 0, Mathf.Max(0, screen.Y - KeepVisible)));
  }

  private void SetKind(HudWindowKind kind)
  {
    if (kind == Kind)
      return;
    Kind = kind;
    RebuildContent();
    Callable.From(FitToContent).CallDeferred();
  }

  /// <summary>
  /// Пересоздать содержимое окна под текущий тип и выбранного игрока.
  /// </summary>
  private void RebuildContent()
  {
    if (_content is not null)
    {
      _host.RemoveChild(_content);
      _content.QueueFree();
    }

    _playerPicker.Visible = Kind == HudWindowKind.OtherHq;
    if (ShownPlayer is { } shown)
    {
      _playerName.Text = shown.Name;
      _playerName.AddThemeColorOverride("font_color", PlayerPalette.MainOf(shown.Color));
    }

    _content = ContentFactory(this);
    _host.AddChild(_content);
    _content.MinimumSizeChanged += LayoutContent;
    LayoutContent();
  }

  /// <summary>
  /// Вписать содержимое в окно: оно масштабируется так, чтобы целиком поместиться, и занимает всё окно
  /// (содержимое <see cref="IFillsHudWindow"/> не масштабируется).
  /// </summary>
  private void LayoutContent()
  {
    if (_content is null)
      return;

    var minimum = _content.GetCombinedMinimumSize();
    var available = _host.Size;
    var scale = 1f;
    if (_content is not IFillsHudWindow && minimum.X > 0 && minimum.Y > 0)
      scale = Mathf.Clamp(Mathf.Min(available.X / minimum.X, available.Y / minimum.Y), MinContentScale, MaxContentScale);

    _content.Position = Vector2.Zero;
    _content.Scale = Vector2.One * scale;
    _content.Size = available / scale;
  }

  /// <summary>
  /// Подогнать размер окна под содержимое в натуральную величину. При первом показе — поставить окно на место.
  /// </summary>
  private void FitToContent()
  {
    if (!IsInsideTree())
      return;

    var content = _content is IFillsHudWindow fill ? fill.InitialSize : _content?.GetCombinedMinimumSize() ?? Vector2.Zero;
    // Начальный размер — по заголовку с полным названием типа, хотя сжать окно потом можно и сильнее.
    _kindPicker.FitToLongestItem = true;
    var header = _header.GetCombinedMinimumSize();
    _kindPicker.FitToLongestItem = false;
    var margins = GetThemeStylebox("panel").GetMinimumSize();
    Size = new Vector2(Mathf.Max(header.X, content.X), header.Y + HeaderSeparation + content.Y) + margins;

    if (_placement is not null || Position == Vector2.Zero)
    {
      var screen = GetViewportRect().Size;
      Position = _placement?.Invoke(Size, screen) ?? (screen - Size) / 2;
      _placement = null;
    }

    // Окно подросло (например, сменили тип) — сдвигаем его, чтобы оно целиком осталось на экране, если помещается.
    var limit = GetViewportRect().Size - Size;
    Position = new Vector2(
      Mathf.Max(0, Mathf.Min(Position.X, limit.X)),
      Mathf.Max(0, Mathf.Min(Position.Y, limit.Y)));
    ClampToViewport();
  }

  private Control CreatePlayerPicker()
  {
    _playerPicker = new HBoxContainer { Name = "PlayerPicker", MouseFilter = MouseFilterEnum.Ignore };
    _playerPicker.AddThemeConstantOverride("separation", 2);

    var previous = CreateIconButton("Previous", (icon, _) => DrawArrow(icon, -1));
    previous.TooltipText = "Предыдущий игрок";
    previous.Pressed += () => StepPlayer(-1);
    _playerPicker.AddChild(previous);

    _playerName = new Label
    {
      CustomMinimumSize = new Vector2(PlayerNameWidth, 0),
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Center,
      ClipText = true,
      MouseFilter = MouseFilterEnum.Ignore,
    };
    _playerName.AddThemeFontSizeOverride("font_size", HeaderFontSize);
    _playerPicker.AddChild(_playerName);

    var next = CreateIconButton("Next", (icon, _) => DrawArrow(icon, 1));
    next.TooltipText = "Следующий игрок";
    next.Pressed += () => StepPlayer(1);
    _playerPicker.AddChild(next);
    return _playerPicker;
  }

  private List<Player> OtherPlayers() => Players.Where(player => player != LayoutOwner).ToList();

  /// <summary>
  /// Показать штаб соседнего по порядку игрока (себя пропускаем).
  /// </summary>
  private void StepPlayer(int direction)
  {
    var others = OtherPlayers();
    if (others.Count == 0)
      return;
    var index = ShownPlayer is { } shown ? others.IndexOf(shown) : -1;
    ShownPlayer = others[((index + direction) % others.Count + others.Count) % others.Count];
    RebuildContent();
  }

  private void UpdatePinState()
  {
    _pin.TooltipText = _pinned ? "Окно закреплено — нажмите, чтобы открепить" : "Закрепить окно: его нельзя будет перемещать и растягивать";
    _grip.MouseDefaultCursorShape = _pinned ? CursorShape.Arrow : CursorShape.Move;
    _grip.TooltipText = _pinned ? string.Empty : "Перетащите, чтобы переместить окно. Края окна растягивают его.";
    if (_pinned)
    {
      _moving = false;
      _resizing = Edge.None;
      MouseDefaultCursorShape = CursorShape.Arrow;
    }

    _grip.QueueRedraw();
    // Значок перерисовываем и тогда, когда состояние задано из кода (без сигнала Toggled).
    foreach (var icon in _pin.GetChildren().OfType<Control>())
      icon.QueueRedraw();
  }

  private void OnGripInput(InputEvent @event)
  {
    switch (@event)
    {
      case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press:
        MoveToFront();
        _moving = !_pinned;
        _pressMouse = press.GlobalPosition;
        _pressRect = new Rect2(Position, Size);
        _grip.AcceptEvent();
        break;
      case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
        _moving = false;
        _grip.AcceptEvent();
        break;
      case InputEventMouseMotion motion when _moving:
        Position = _pressRect.Position + motion.GlobalPosition - _pressMouse;
        ClampToViewport();
        _grip.AcceptEvent();
        break;
    }
  }

  private void Resize(Vector2 mouse)
  {
    var delta = mouse - _pressMouse;
    var minimum = GetCombinedMinimumSize();
    minimum.Y = Mathf.Max(minimum.Y, MinHeight);
    var left = _pressRect.Position.X;
    var top = _pressRect.Position.Y;
    var right = _pressRect.End.X;
    var bottom = _pressRect.End.Y;
    if (_resizing.HasFlag(Edge.Left))
      left = Mathf.Min(left + delta.X, right - minimum.X);
    if (_resizing.HasFlag(Edge.Right))
      right = Mathf.Max(right + delta.X, left + minimum.X);
    if (_resizing.HasFlag(Edge.Top))
      top = Mathf.Min(top + delta.Y, bottom - minimum.Y);
    if (_resizing.HasFlag(Edge.Bottom))
      bottom = Mathf.Max(bottom + delta.Y, top + minimum.Y);

    Position = new Vector2(left, top);
    Size = new Vector2(right - left, bottom - top);
  }

  private Edge EdgeAt(Vector2 local)
  {
    var edge = Edge.None;
    if (local.X < ResizeMargin)
      edge |= Edge.Left;
    else if (local.X > Size.X - ResizeMargin)
      edge |= Edge.Right;
    if (local.Y < ResizeMargin)
      edge |= Edge.Top;
    else if (local.Y > Size.Y - ResizeMargin)
      edge |= Edge.Bottom;
    return edge;
  }

  private static CursorShape CursorOf(Edge edge) => edge switch
  {
    Edge.Left | Edge.Top or Edge.Right | Edge.Bottom => CursorShape.Fdiagsize,
    Edge.Right | Edge.Top or Edge.Left | Edge.Bottom => CursorShape.Bdiagsize,
    Edge.Left or Edge.Right => CursorShape.Hsize,
    Edge.Top or Edge.Bottom => CursorShape.Vsize,
    _ => CursorShape.Arrow,
  };

  private static Button CreateIconButton(string name, Action<Control, Button> draw)
  {
    var button = new Button
    {
      Name = name,
      FocusMode = FocusModeEnum.None,
      CustomMinimumSize = new Vector2(HeaderButtonSize, HeaderButtonSize),
    };
    // Отступы темы кнопок рассчитаны на текст; у кнопки-значка их нет, чтобы она оставалась квадратной.
    foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
    {
      if (button.GetThemeStylebox(state).Duplicate() is StyleBox style)
      {
        style.ContentMarginLeft = style.ContentMarginRight = style.ContentMarginTop = style.ContentMarginBottom = 0;
        button.AddThemeStyleboxOverride(state, style);
      }
    }

    var icon = new Control { MouseFilter = MouseFilterEnum.Ignore };
    icon.SetAnchorsPreset(LayoutPreset.FullRect);
    icon.Draw += () => draw(icon, button);
    button.AddChild(icon);
    button.Toggled += _ => icon.QueueRedraw();
    return button;
  }

  /// <summary>
  /// Канцелярская кнопка: отжата — лежит наискосок, нажата (окно закреплено) — воткнута прямо и светится.
  /// </summary>
  private static void DrawPin(Control icon, Button button)
  {
    var pinned = button.ButtonPressed;
    var color = pinned ? UiStyle.Glow : IconColor;
    var unit = Mathf.Min(icon.Size.X, icon.Size.Y) / HeaderButtonSize;
    icon.DrawSetTransform(icon.Size / 2, pinned ? 0 : Mathf.Pi / 4, Vector2.One * unit);
    icon.DrawRect(new Rect2(-4.5f, -9, 9, 3), color);
    icon.DrawRect(new Rect2(-2.5f, -6, 5, 6), color);
    icon.DrawRect(new Rect2(-6.5f, 0, 13, 2.5f), color);
    icon.DrawLine(new Vector2(0, 2.5f), new Vector2(0, 9.5f), color, 1.5f);
    icon.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
  }

  private static void DrawClose(Control icon, Button _)
  {
    var center = icon.Size / 2;
    var half = Mathf.Min(icon.Size.X, icon.Size.Y) * 0.2f;
    icon.DrawLine(center + new Vector2(-half, -half), center + new Vector2(half, half), IconColor, 2);
    icon.DrawLine(center + new Vector2(-half, half), center + new Vector2(half, -half), IconColor, 2);
  }

  private static void DrawArrow(Control icon, int direction)
  {
    var center = icon.Size / 2;
    var half = Mathf.Min(icon.Size.X, icon.Size.Y) * 0.2f;
    icon.DrawColoredPolygon(
    [
      center + new Vector2(direction * half, 0),
      center + new Vector2(-direction * half, -half),
      center + new Vector2(-direction * half, half),
    ], IconColor);
  }

  /// <summary>
  /// Точки-«насечки» на свободной части заголовка: за неё окно перетаскивают. У закреплённого окна их нет.
  /// </summary>
  private void DrawGrip()
  {
    if (_pinned)
      return;
    var size = _grip.Size;
    for (var x = 4f; x < size.X - 4; x += 6)
    {
      _grip.DrawRect(new Rect2(x, size.Y / 2 - 4, 2, 2), GripColor);
      _grip.DrawRect(new Rect2(x, size.Y / 2 + 2, 2, 2), GripColor);
    }
  }
}
