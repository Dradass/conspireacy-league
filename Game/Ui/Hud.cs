using ConspiracyLeague.Core.Game;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Интерфейс поверх стола: игроки с балансами и кнопка перехода хода слева внизу, колоды справа внизу
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
  private Label _turn = null!;
  private PanelContainer _messagePanel = null!;
  private Label _message = null!;
  private Tween? _messageTween;

  /// <summary>
  /// Сессия игры: игроки, чьи балансы показываются, и очерёдность ходов.
  /// </summary>
  public GameSession Session { get; init; } = null!;

  private TurnOrder Turns => Session.Turns;

  /// <summary>
  /// Колоды, из которых тянут карточки.
  /// </summary>
  public IReadOnlyList<DeckView> Decks { get; init; } = [];

  public override void _Ready()
  {
    var root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Ignore };
    root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    AddChild(root);

    root.AddChild(CreatePlayers());
    root.AddChild(CreateDecks());
    root.AddChild(CreateMessage());

    foreach (var player in Turns.Players)
    {
      player.MoneyChanged += OnMoneyChanged;
      OnMoneyChanged(player);
    }

    Turns.TurnChanged += OnTurnChanged;
    OnTurnChanged(Turns.CurrentPlayer);
  }

  public override void _ExitTree()
  {
    foreach (var player in Turns.Players)
      player.MoneyChanged -= OnMoneyChanged;
    Turns.TurnChanged -= OnTurnChanged;
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

    _turn = new Label();
    _turn.AddThemeFontSizeOverride("font_size", 22);
    column.AddChild(_turn);

    foreach (var player in Turns.Players)
    {
      var balance = new Label { Name = $"Balance{player.Color}" };
      balance.AddThemeFontSizeOverride("font_size", 24);
      balance.AddThemeColorOverride("font_color", PlayerPalette.MainOf(player.Color));
      column.AddChild(balance);
      _balances[player] = balance;
    }

    var endTurn = new Button { Name = "EndTurn", Text = "Переход хода", FocusMode = Control.FocusModeEnum.None };
    endTurn.AddThemeFontSizeOverride("font_size", 20);
    endTurn.Pressed += Turns.EndTurn;
    column.AddChild(endTurn);
    return panel;
  }

  private Control CreateDecks()
  {
    var row = new HBoxContainer
    {
      Name = "Decks",
      MouseFilter = Control.MouseFilterEnum.Ignore,
      AnchorLeft = 1,
      AnchorRight = 1,
      AnchorTop = 1,
      AnchorBottom = 1,
      OffsetLeft = -Margin,
      OffsetRight = -Margin,
      OffsetTop = -Margin,
      OffsetBottom = -Margin,
      GrowHorizontal = Control.GrowDirection.Begin,
      GrowVertical = Control.GrowDirection.Begin,
    };
    row.AddThemeConstantOverride("separation", (int)(Margin / 2));
    foreach (var deck in Decks)
      row.AddChild(deck);

    return row;
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

  private void OnMoneyChanged(Player player) => _balances[player].Text = $"{player.Name}: {player.Money}$";

  private void OnTurnChanged(Player current)
  {
    _turn.Text = $"Ходит: {current.Name}";
    _turn.AddThemeColorOverride("font_color", PlayerPalette.MainOf(current.Color));
    foreach (var (player, balance) in _balances)
      balance.Modulate = player == current ? Colors.White : Colors.White with { A = WaitingPlayerAlpha };
  }
}
