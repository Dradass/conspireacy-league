using ConspiracyLeague.Core.Bots;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Protocol;
using ConspiracyLeague.Game.Net;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Лобби сетевой игры: кто сидит за столом и на связи ли он. Создатель стола добавляет и убирает места, выбирает режим
/// и начинает партию; остальные ждут. Когда партия начнётся и придёт её вид, лобби сменяется партией (<see cref="Main"/>).
/// </summary>
public partial class NetworkLobby : Control
{
  private const string StartMenuScene = "res://Game/StartMenu.tscn";
  private const int FontSize = 20;
  private const float PanelWidth = 560f;

  private Label _status = null!;
  private VBoxContainer _seats = null!;
  private ScrollContainer _seatsScroll = null!;

  /// <summary>
  /// Высота строки места (по кнопке «Убрать») и сколько строк видно без прокрутки.
  /// </summary>
  private const float SeatRowHeight = 38f;
  private const int SeatSeparation = 4;
  private const int VisibleSeats = 4;
  private Control _hostControls = null!;
  private OptionButton _mode = null!;
  private OptionButton _level = null!;
  private CheckBox _failure = null!;
  private SpinBox _turnSeconds = null!;
  private bool _started;

  /// <summary>
  /// Подключение к столу. Лобби передаёт его партии; если партия не началась — закрывает при выходе.
  /// </summary>
  public NetworkConnection Connection { get; init; } = null!;

  public override void _Ready()
  {
    UiStyle.Apply();
    SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    AddChild(new MenuBackdrop { Name = "Backdrop" });

    var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
    AddChild(center);
    var panel = new PanelContainer { CustomMinimumSize = new Vector2(PanelWidth, 0) };
    panel.AddThemeStyleboxOverride("panel", UiStyle.Panel(margin: 22, alpha: 0.94f));
    center.AddChild(panel);
    var column = new VBoxContainer();
    column.AddThemeConstantOverride("separation", 12);
    panel.AddChild(column);

    var title = Text("Стол", 30);
    UiStyle.MakeTitle(title);
    column.AddChild(title);
    if (Connection.Server is { } server)
    {
      var addresses = NetworkConnection.LocalAddresses();
      column.AddChild(Text(addresses.Count == 0
        ? $"Стол создан на порту {server.Port}."
        : $"Адрес для друзей в локальной сети: {string.Join(", ", addresses.Select(address => $"{address}:{server.Port}"))}", 16));
    }

    _status = Text("Подключение…", 18);
    column.AddChild(_status);
    // Места — в прокручиваемом списке: при 8 игроках лобби иначе не поместилось бы на экране.
    _seatsScroll = new ScrollContainer { Name = "SeatsScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
    column.AddChild(_seatsScroll);
    _seats = new VBoxContainer { Name = "Seats", SizeFlagsHorizontal = SizeFlags.ExpandFill };
    _seats.AddThemeConstantOverride("separation", SeatSeparation);
    _seatsScroll.AddChild(_seats);

    _hostControls = BuildHostControls();
    _hostControls.Visible = false;
    column.AddChild(_hostControls);
    column.AddChild(Button("Leave", "Выйти", Leave));

    var table = Connection.Table;
    table.LobbyChanged += OnLobbyChanged;
    table.Notice += ShowNotice;
    table.Disconnecting += OnDisconnecting;
    Connection.Link.Disconnected += OnLinkDisconnected;
    Connection.Link.Connected += OnLinkConnected;
  }

  public override void _ExitTree()
  {
    var table = Connection.Table;
    table.LobbyChanged -= OnLobbyChanged;
    table.Notice -= ShowNotice;
    table.Disconnecting -= OnDisconnecting;
    Connection.Link.Disconnected -= OnLinkDisconnected;
    Connection.Link.Connected -= OnLinkConnected;
    if (!_started)
      Connection.Dispose();
  }

  public override void _Process(double delta)
  {
    Connection.Poll();
    // Партия началась и её вид пришёл — переходим к партии. Подключение переходит к ней.
    if (!_started && Connection.Table.Seat is not null && Connection.Table.Game.Mirror is not null)
      StartGame();
  }

  private Control BuildHostControls()
  {
    var box = new VBoxContainer { Name = "HostControls" };
    box.AddThemeConstantOverride("separation", 8);

    var bots = new HBoxContainer();
    bots.AddThemeConstantOverride("separation", 8);
    _level = new OptionButton { FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    _level.AddThemeFontSizeOverride("font_size", FontSize);
    foreach (var level in Enum.GetValues<BotLevel>())
      _level.AddItem(LevelName(level), (int)level);
    bots.AddChild(_level);
    bots.AddChild(Button("AddBot", "Добавить бота", () => Connection.Table.AddBot((BotLevel)_level.GetSelectedId())));
    box.AddChild(bots);

    _mode = new OptionButton { FocusMode = FocusModeEnum.None };
    _mode.AddThemeFontSizeOverride("font_size", FontSize);
    _mode.AddItem("Заработок денег", (int)GameMode.Money);
    _mode.AddItem("Развитие теорий", (int)GameMode.TheoryDevelopment);
    _mode.AddItem("Гонка", (int)GameMode.Race);
    _mode.Selected = _mode.GetItemIndex((int)GameMode.TheoryDevelopment);
    box.AddChild(_mode);

    _failure = new CheckBox { Text = "Последствия провала на низких гранях", ButtonPressed = GameOptions.CriticalFailure, FocusMode = FocusModeEnum.None };
    _failure.AddThemeFontSizeOverride("font_size", FontSize);
    box.AddChild(_failure);

    // Таймер: сколько ждать хода каждого игрока; 0 — пока игрок сам не походит или не спасует.
    var timer = new HBoxContainer();
    timer.AddThemeConstantOverride("separation", 8);
    var caption = new Label { Text = "Таймер хода, с (0 — без ограничения)", SizeFlagsHorizontal = SizeFlags.ExpandFill };
    caption.AddThemeFontSizeOverride("font_size", FontSize);
    timer.AddChild(caption);
    _turnSeconds = new SpinBox
    {
      Name = "TurnSeconds",
      MinValue = 0,
      MaxValue = LobbyStart.MaxTurnSeconds,
      Step = 1,
      Value = GameOptions.TurnSeconds,
      Alignment = HorizontalAlignment.Center,
      CustomMinimumSize = new Vector2(120, 0),
    };
    _turnSeconds.GetLineEdit().AddThemeFontSizeOverride("font_size", FontSize);
    timer.AddChild(_turnSeconds);
    box.AddChild(timer);

    box.AddChild(Button("Start", "Начать партию", StartPressed));
    return box;
  }

  private void StartPressed()
  {
    var seconds = (int)_turnSeconds.Value;
    GameOptions.TurnSeconds = seconds;
    GameOptions.Save();
    Connection.Table.Start((GameMode)_mode.GetSelectedId(), _failure.ButtonPressed, seconds);
  }

  private void OnLobbyChanged(LobbyState lobby)
  {
    _status.Text = lobby.Started ? "Партия начинается…" : Connection.Table.IsHost
      ? "Добавьте ботов или дождитесь друзей и начните партию."
      : "Ждём, пока создатель стола начнёт партию.";
    _hostControls.Visible = Connection.Table.IsHost && !lobby.Started;

    foreach (var child in _seats.GetChildren())
      child.QueueFree();
    for (var index = 0; index < lobby.Seats.Count; index++)
    {
      var seat = lobby.Seats[index];
      var row = new HBoxContainer();
      row.AddThemeConstantOverride("separation", 8);
      var who = seat.IsBot ? $"бот, {LevelName(seat.BotLevel).ToLowerInvariant()}" : seat.Connected ? "на связи" : "нет связи";
      var label = Text($"{index + 1}. {seat.Name}{(seat.IsHost ? " (создатель)" : "")}{(index == lobby.You ? " — вы" : "")} · {who}", FontSize);
      label.HorizontalAlignment = HorizontalAlignment.Left;
      label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
      row.AddChild(label);
      if (Connection.Table.IsHost && !lobby.Started && index != lobby.You)
      {
        var removed = index;
        var remove = Button($"Remove{index}", "Убрать", () => Connection.Table.RemoveSeat(removed));
        remove.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        row.AddChild(remove);
      }
      row.CustomMinimumSize = new Vector2(0, SeatRowHeight);
      _seats.AddChild(row);
    }

    // Список высотой по числу мест, но не больше нескольких строк — дальше прокрутка.
    var rows = Math.Min(lobby.Seats.Count, VisibleSeats);
    _seatsScroll.CustomMinimumSize = new Vector2(0, rows * SeatRowHeight + Math.Max(0, rows - 1) * SeatSeparation);
  }

  private void ShowNotice(string text) => _status.Text = text;

  private void OnDisconnecting(string reason, bool final) => _status.Text = final ? $"{reason}" : $"{reason} Переподключение…";

  private void OnLinkDisconnected(string reason)
  {
    if (!Connection.Table.Closed)
      _status.Text = $"{reason} Переподключение…";
  }

  private void OnLinkConnected() => _status.Text = "Связь установлена.";

  private void StartGame()
  {
    _started = true;
    var tree = GetTree();
    var game = new Main
    {
      Name = "Main",
      Net = NetworkGame.Create(Connection),
      RadialField = GameOptions.RadialField,
    };
    tree.Root.AddChild(game);
    tree.CurrentScene = game;
    QueueFree();
  }

  private void Leave() => GetTree().ChangeSceneToFile(StartMenuScene);

  private static string LevelName(BotLevel level) => level switch
  {
    BotLevel.Easy => "Лёгкий",
    BotLevel.Medium => "Средний",
    BotLevel.Hard => "Тяжёлый",
    _ => level.ToString(),
  };

  private static Label Text(string text, int fontSize)
  {
    var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
    label.AddThemeFontSizeOverride("font_size", fontSize);
    return label;
  }

  private static Button Button(string name, string text, Action pressed)
  {
    var button = new Button { Name = name, Text = text, FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    button.AddThemeFontSizeOverride("font_size", FontSize);
    button.Pressed += pressed;
    return button;
  }
}
