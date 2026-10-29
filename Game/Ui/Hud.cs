using ConspiracyLeague.Core.Game;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Интерфейс поверх стола: раунд и фаза, игроки с балансами, кнопки «Пас» и «Переход хода» слева внизу, рука игрока справа внизу
/// и сообщения сверху.
/// </summary>
public partial class Hud : CanvasLayer
{
  private const float Margin = 24f;
  private const double MessageDuration = 3;
  private const double MessageFade = 0.4;

  private static readonly Color PanelColor = new(0.08f, 0.1f, 0.09f, 0.85f);
  private const float WaitingPlayerAlpha = 0.55f;

  private readonly Dictionary<Player, Label> _balances = new();
  private Label _round = null!;
  private Label _turn = null!;
  private Label _actions = null!;
  private Button _pass = null!;
  private Button _endTurn = null!;
  private PanelContainer _messagePanel = null!;
  private Label _message = null!;
  private Tween? _messageTween;

  /// <summary>
  /// Сессия игры: игроки, чьи балансы показываются, и очерёдность ходов.
  /// </summary>
  public GameSession Session { get; init; } = null!;

  private TurnOrder Turns => Session.Turns;

  /// <summary>
  /// Рука игрока, который сейчас ходит.
  /// </summary>
  public HandView Hand { get; init; } = null!;

  public override void _Ready()
  {
    var root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Ignore };
    root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    AddChild(root);

    root.AddChild(CreatePlayers());
    root.AddChild(CreateHand());
    root.AddChild(CreateMessage());

    foreach (var player in Turns.Players)
    {
      player.MoneyChanged += OnMoneyChanged;
      OnMoneyChanged(player);
    }

    Turns.TurnChanged += OnTurnChanged;
    Turns.Passed += OnPassed;
    Session.PhaseChanged += OnPhaseChanged;
    Session.ActionsLeftChanged += OnActionsLeftChanged;
    Session.AutoPassed += OnAutoPassed;
    Session.PendingCheckChanged += OnPendingCheckChanged;
    OnPhaseChanged(Session.Phase);
  }

  public override void _ExitTree()
  {
    foreach (var player in Turns.Players)
      player.MoneyChanged -= OnMoneyChanged;
    Turns.TurnChanged -= OnTurnChanged;
    Turns.Passed -= OnPassed;
    Session.PhaseChanged -= OnPhaseChanged;
    Session.ActionsLeftChanged -= OnActionsLeftChanged;
    Session.AutoPassed -= OnAutoPassed;
    Session.PendingCheckChanged -= OnPendingCheckChanged;
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

  private Control CreatePlayers()
  {
    var panel = CreatePanel("Players");
    panel.AnchorTop = 1;
    panel.AnchorBottom = 1;
    panel.OffsetLeft = Margin;
    panel.OffsetTop = -Margin;
    panel.OffsetBottom = -Margin;
    panel.GrowVertical = Control.GrowDirection.Begin;

    var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
    column.AddThemeConstantOverride("separation", 6);
    panel.AddChild(column);

    _round = new Label();
    _round.AddThemeFontSizeOverride("font_size", 18);
    column.AddChild(_round);

    _turn = new Label();
    _turn.AddThemeFontSizeOverride("font_size", 22);
    column.AddChild(_turn);

    _actions = new Label();
    _actions.AddThemeFontSizeOverride("font_size", 18);
    column.AddChild(_actions);

    foreach (var player in Turns.Players)
    {
      var balance = new Label { Name = $"Balance{player.Color}" };
      balance.AddThemeFontSizeOverride("font_size", 24);
      balance.AddThemeColorOverride("font_color", PlayerPalette.MainOf(player.Color));
      column.AddChild(balance);
      _balances[player] = balance;
    }

    var buttons = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
    buttons.AddThemeConstantOverride("separation", 6);
    column.AddChild(buttons);

    _pass = new Button { Name = "Pass", Text = "Пас", FocusMode = Control.FocusModeEnum.None };
    _pass.AddThemeFontSizeOverride("font_size", 20);
    _pass.Pressed += Session.Pass;
    buttons.AddChild(_pass);

    _endTurn = new Button { Name = "EndTurn", Text = "Переход хода", FocusMode = Control.FocusModeEnum.None };
    _endTurn.AddThemeFontSizeOverride("font_size", 20);
    _endTurn.Pressed += Session.EndTurn;
    buttons.AddChild(_endTurn);
    return panel;
  }

  private Control CreateHand()
  {
    Hand.AnchorLeft = 1;
    Hand.AnchorRight = 1;
    Hand.AnchorTop = 1;
    Hand.AnchorBottom = 1;
    Hand.OffsetLeft = -Margin;
    Hand.OffsetRight = -Margin;
    Hand.OffsetTop = -Margin;
    Hand.OffsetBottom = -Margin;
    Hand.GrowHorizontal = Control.GrowDirection.Begin;
    Hand.GrowVertical = Control.GrowDirection.Begin;
    return Hand;
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
    var style = new StyleBoxFlat { BgColor = PanelColor };
    style.SetCornerRadiusAll(8);
    style.SetContentMarginAll(12);

    var panel = new PanelContainer { Name = name, MouseFilter = Control.MouseFilterEnum.Ignore };
    panel.AddThemeStyleboxOverride("panel", style);
    return panel;
  }

  private void OnMoneyChanged(Player player) =>
    _balances[player].Text = $"{player.Name}: {player.Money}$" + (Turns.HasPassed(player) ? " — пас" : string.Empty);

  private void OnPassed(Player player) => OnMoneyChanged(player);

  private void OnAutoPassed(Player player) =>
    ShowMessage($"{player.Name} пасует автоматически: рука пуста, карточки личного штаба сыграны, на рынке нет карточек.");

  private void OnActionsLeftChanged(int actionsLeft)
  {
    _actions.Visible = Session.Phase == GamePhase.PlayerTurns;
    _actions.Text = $"Действий: {actionsLeft} из {GameSession.ActionsPerTurn}";
  }

  private void OnPendingCheckChanged(SuccessCheck? check) => UpdateButtons();

  /// <summary>
  /// Пасовать и передавать ход можно только во время ходов игроков и не посреди проверки успеха.
  /// </summary>
  private void UpdateButtons()
  {
    var canAct = Session.Phase == GamePhase.PlayerTurns && Session.PendingCheck is null;
    _pass.Disabled = !canAct;
    _endTurn.Disabled = !canAct;
  }

  private void OnPhaseChanged(GamePhase phase)
  {
    UpdateButtons();
    _round.Text = $"Раунд {Session.Round}: {NameOf(phase)}";
    OnActionsLeftChanged(Session.ActionsLeft);
    foreach (var player in Turns.Players)
      OnMoneyChanged(player);
    OnTurnChanged(Turns.CurrentPlayer);
  }

  private static string NameOf(GamePhase phase) => phase switch
  {
    GamePhase.Setup => "подготовка",
    GamePhase.PlayerTurns => "ходы игроков",
    GamePhase.HeadquartersCheck => "проверка штабов теорий",
    GamePhase.AdeptIncome => "адепты приносят деньги",
    GamePhase.CardsRefresh => "обновление карточек",
    GamePhase.MarketRefresh => "обновление рынка",
    _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
  };

  private void OnTurnChanged(Player current)
  {
    _turn.Text = Session.Phase == GamePhase.PlayerTurns ? $"Ходит: {current.Name}" : "Конец раунда";
    _turn.AddThemeColorOverride("font_color", PlayerPalette.MainOf(current.Color));
    foreach (var (player, balance) in _balances)
      balance.Modulate = player == current ? Colors.White : Colors.White with { A = WaitingPlayerAlpha };
  }
}
