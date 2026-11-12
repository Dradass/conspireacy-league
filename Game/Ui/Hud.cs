using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Game.Table;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Интерфейс поверх стола: окна интерфейса (<see cref="HudWindow"/>) и сообщения сверху. Когда партия завершена —
/// итоги (<see cref="ResultsPanel"/>).
/// </summary>
/// <remarks>
/// У каждого игрока своя раскладка окон: показывается раскладка того, чьё действие сейчас ожидается. По умолчанию
/// в ней три закреплённых окна — информация об игроках справа вверху, рука справа внизу и личный штаб слева внизу;
/// игрок может откреплять, двигать, растягивать и закрывать их и добавлять новые через меню («Добавить окно»).
/// </remarks>
public partial class Hud : CanvasLayer
{
  private const float Margin = 24f;
  private const double MessageDuration = 3;
  private const double MessageFade = 0.4;
  private const float ReminderOffset = 64f;
  private const float TurnButtonsGap = 8f;
  private const HudWindowKind NewWindowKind = HudWindowKind.Market;

  private readonly Dictionary<Player, Control> _layouts = new();
  private PanelContainer _messagePanel = null!;
  private Label _message = null!;
  private Tween? _messageTween;
  private PanelContainer _reminderPanel = null!;
  private Label _reminder = null!;
  private Control _root = null!;
  private Player? _layoutPlayer;

  /// <summary>
  /// Сессия игры.
  /// </summary>
  public GameSession Session { get; init; } = null!;

  private TurnOrder Turns => Session.Turns;

  /// <summary>
  /// Общее состояние окон руки.
  /// </summary>
  public HandHub Hand { get; init; } = null!;

  /// <summary>
  /// Общее состояние окон личного штаба.
  /// </summary>
  public PersonalHqHub PersonalHq { get; init; } = null!;

  /// <summary>
  /// Связь окон рынка с контроллерами.
  /// </summary>
  public MarketHub Market { get; init; } = null!;

  /// <summary>
  /// Связь окон «Теории» с контроллерами.
  /// </summary>
  public TheoriesHub Theories { get; init; } = null!;

  /// <summary>
  /// Стол: окна «Теории» показывают его общую зону.
  /// </summary>
  public Board Board { get; init; } = null!;

  public override void _Ready()
  {
    var root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Ignore };
    root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    AddChild(root);
    _root = root;

    root.AddChild(CreateLayouts());
    root.AddChild(CreateMessage());
    root.AddChild(CreateReminder());
    var theoryGraph = new TheoryGraphPanel { Name = "TheoryGraph" };
    // Кнопки хода — под кнопкой меню; раскрытое меню ложится поверх них.
    root.AddChild(CreateTurnButtons());
    var gameMenu = new GameMenu { Name = "GameMenu" };
    gameMenu.TheoriesRequested += theoryGraph.Open;
    gameMenu.AddWindowRequested += AddWindow;
    root.AddChild(gameMenu);
    // Граф поверх меню и интерфейса, но под окном информации о карточке (слой 3).
    theoryGraph.BackRequested += gameMenu.ShowMenu;
    root.AddChild(theoryGraph);

    Turns.TurnChanged += OnTurnChanged;
    Turns.FirstPlayerChanged += OnFirstPlayerChanged;
    Session.PhaseChanged += OnPhaseChanged;
    Session.AutoPassed += OnAutoPassed;
    Session.LastRoundDeclared += OnLastRoundDeclared;
    Session.PendingTheoryTriggerChanged += OnPendingTheoryTriggerChanged;
    GetViewport().SizeChanged += OnViewportSizeChanged;
    ShowLayoutOfActor();
  }

  public override void _ExitTree()
  {
    Turns.TurnChanged -= OnTurnChanged;
    Turns.FirstPlayerChanged -= OnFirstPlayerChanged;
    Session.PhaseChanged -= OnPhaseChanged;
    Session.AutoPassed -= OnAutoPassed;
    Session.LastRoundDeclared -= OnLastRoundDeclared;
    Session.PendingTheoryTriggerChanged -= OnPendingTheoryTriggerChanged;
    GetViewport().SizeChanged -= OnViewportSizeChanged;
  }

  /// <summary>
  /// Показать сообщение игроку.
  /// </summary>
  public void ShowMessage(string text)
  {
    _message.Text = text;
    _messageTween?.Kill();
    _messagePanel.Modulate = Colors.White;
    _messagePanel.Visible = true;
    _messageTween = CreateTween();
    _messageTween.TweenInterval(MessageDuration);
    _messageTween.TweenProperty(_messagePanel, "modulate:a", 0f, MessageFade);
    _messageTween.TweenCallback(Callable.From(() => _messagePanel.Visible = false));
  }

  /// <summary>
  /// Напоминание о действии основной теории, которое нужно сыграть сейчас. В отличие от <see cref="ShowMessage"/>
  /// не гаснет само: висит, пока его не сменят или не уберут (<c>null</c>).
  /// </summary>
  public void ShowReminder(string? text)
  {
    _reminderPanel.Visible = text is not null;
    if (text is not null)
      _reminder.Text = text;
  }

  /// <summary>
  /// Раскладки окон всех игроков; видна только раскладка того, чьё действие ожидается.
  /// </summary>
  private Control CreateLayouts()
  {
    var windows = new Control { Name = "Windows", MouseFilter = Control.MouseFilterEnum.Ignore };
    windows.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    foreach (var player in Turns.Players)
    {
      var layout = new Control { Name = $"Layout{player.Color}", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
      layout.SetAnchorsPreset(Control.LayoutPreset.FullRect);
      windows.AddChild(layout);
      _layouts[player] = layout;

      // Бывшие закреплённые панели — теперь закреплённые окна на прежних местах.
      layout.AddChild(CreateWindow(player, HudWindowKind.Players, pinned: true,
        (size, screen) => new Vector2(screen.X - size.X - Margin, Margin)));
      layout.AddChild(CreateWindow(player, HudWindowKind.Hand, pinned: true,
        (size, screen) => screen - size - new Vector2(Margin, Margin)));
    }

    return windows;
  }

  private HudWindow CreateWindow(Player owner, HudWindowKind kind, bool pinned, Func<Vector2, Vector2, Vector2>? placement = null) => new()
  {
    Name = $"Window{kind}",
    LayoutOwner = owner,
    Players = Turns.Players.ToList(),
    InitialKind = kind,
    Pinned = pinned,
    Placement = placement,
    ContentFactory = CreateContent,
  };

  private Control CreateContent(HudWindow window) => window.Kind switch
  {
    HudWindowKind.Market => new MarketView { Name = "Market", Session = Session, Hub = Market },
    HudWindowKind.Theories => new TheoriesView { Name = "Theories", Board = Board, Hub = Theories },
    HudWindowKind.Hand => new HandView { Name = "Hand", Hub = Hand },
    HudWindowKind.PersonalHq => new PersonalHqView { Name = "PersonalHq", Session = Session, Hub = PersonalHq },
    HudWindowKind.OtherHq => new PersonalHqView { Name = "OtherHq", Session = Session, Hub = PersonalHq, FixedPlayer = window.ShownPlayer },
    HudWindowKind.Players => new PlayersInfoView { Name = "Players", Session = Session },
    _ => throw new ArgumentOutOfRangeException(nameof(window), window.Kind, null),
  };

  /// <summary>
  /// Кнопка меню «Добавить окно»: новое незакреплённое окно по центру экрана в раскладке того, чья она сейчас.
  /// </summary>
  private void AddWindow()
  {
    if (_layoutPlayer is not { } player)
      return;
    var window = CreateWindow(player, NewWindowKind, pinned: false);
    _layouts[player].AddChild(window);
  }

  private void OnViewportSizeChanged()
  {
    foreach (var window in _layouts.Values.SelectMany(layout => layout.GetChildren().OfType<HudWindow>()))
      window.ClampToViewport();
  }

  /// <summary>
  /// Показать раскладку окон игрока, чьё действие сейчас ожидается: обычно того, кто ходит, а пока владелец основной
  /// теории выбирает действие после дочерней карточки — его.
  /// </summary>
  private void ShowLayoutOfActor()
  {
    var actor = Session.PendingTheoryTrigger?.Owner ?? Turns.CurrentPlayer;
    if (actor == _layoutPlayer)
      return;
    _layoutPlayer = actor;
    foreach (var (player, layout) in _layouts)
      layout.Visible = player == actor;
  }

  private Control CreateTurnButtons()
  {
    var buttons = new TurnButtons { Name = "TurnButtons", Session = Session };
    buttons.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
    buttons.OffsetLeft = GameMenu.Margin;
    buttons.OffsetTop = GameMenu.Margin + GameMenu.IconSize + TurnButtonsGap;
    return buttons;
  }

  private Control CreateReminder()
  {
    _reminderPanel = CreatePanel("Reminder");
    _reminderPanel.Visible = false;
    _reminderPanel.AnchorLeft = 0.5f;
    _reminderPanel.AnchorRight = 0.5f;
    // Под сообщениями, чтобы они не перекрывали друг друга.
    _reminderPanel.OffsetTop = Margin + ReminderOffset;
    _reminderPanel.GrowHorizontal = Control.GrowDirection.Both;

    _reminder = new Label { HorizontalAlignment = HorizontalAlignment.Center };
    _reminder.AddThemeFontSizeOverride("font_size", 18);
    _reminderPanel.AddChild(_reminder);
    return _reminderPanel;
  }

  private Control CreateMessage()
  {
    _messagePanel = CreatePanel("Message");
    _messagePanel.Visible = false;
    _messagePanel.AnchorLeft = 0.5f;
    _messagePanel.AnchorRight = 0.5f;
    _messagePanel.OffsetTop = Margin;
    _messagePanel.GrowHorizontal = Control.GrowDirection.Both;

    _message = new Label { HorizontalAlignment = HorizontalAlignment.Center };
    _message.AddThemeFontSizeOverride("font_size", 18);
    _messagePanel.AddChild(_message);
    return _messagePanel;
  }

  private static PanelContainer CreatePanel(string name)
  {
    var panel = new PanelContainer { Name = name, MouseFilter = Control.MouseFilterEnum.Ignore };
    panel.AddThemeStyleboxOverride("panel", UiStyle.Panel(margin: 12, alpha: 0.85f));
    return panel;
  }

  private void OnFirstPlayerChanged(Player first) =>
    ShowMessage($"Жетон первого игрока у игрока {first.Name}: он начинает раунд.");

  private void OnAutoPassed(Player player) =>
    ShowMessage($"{player.Name} пасует автоматически: рука пуста, карточки личного штаба сыграны, на рынке нет карточек.");

  private void OnPendingTheoryTriggerChanged(TheoryTrigger? trigger) => ShowLayoutOfActor();

  private void OnPhaseChanged(GamePhase phase)
  {
    ShowLayoutOfActor();
    if (phase == GamePhase.GameOver)
      _root.AddChild(new ResultsPanel { Name = "Results", Session = Session });
  }

  /// <summary>
  /// Режим гонки: теория дошла до финиша — раунд объявлен последним.
  /// </summary>
  private void OnLastRoundDeclared(TheoryColumn column) =>
    ShowMessage($"Теория «{column.Theory}» дошла до финиша! Этот раунд — последний.");

  private void OnTurnChanged(Player _) => ShowLayoutOfActor();
}
