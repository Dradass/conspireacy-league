using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Стартовое меню: выбор количества игроков, начало партии и выход из игры.
/// </summary>
public partial class StartMenu : Control
{
  /// <summary>
  /// Наименьшее количество игроков в партии.
  /// </summary>
  public const int MinPlayers = 2;

  private const int DefaultPlayers = 4;
  private const float MenuWidth = 420f;
  private const int TitleFontSize = 48;
  private const int LabelFontSize = 24;
  private const int ButtonFontSize = 28;
  private const float ButtonHeight = 60f;

  private static readonly Color BackgroundColor = new("26342c");
  private static readonly Color TitleColor = new("e0b84f");

  private Label _playersLabel = null!;
  private HSlider _players = null!;
  private OptionButton _mode = null!;
  private CheckButton _failureConsequences = null!;

  /// <summary>
  /// Выбранный режим партии.
  /// </summary>
  private GameMode SelectedMode => (GameMode)_mode.GetSelectedId();

  public override void _Ready()
  {
    SetAnchorsPreset(LayoutPreset.FullRect);
    AddChild(new ColorRect { Color = BackgroundColor, AnchorRight = 1, AnchorBottom = 1 });

    var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
    AddChild(center);

    var column = new VBoxContainer { CustomMinimumSize = new Vector2(MenuWidth, 0) };
    column.AddThemeConstantOverride("separation", 18);
    center.AddChild(column);

    var title = new Label { Text = "Conspiracy League", HorizontalAlignment = HorizontalAlignment.Center };
    title.AddThemeFontSizeOverride("font_size", TitleFontSize);
    title.AddThemeColorOverride("font_color", TitleColor);
    column.AddChild(title);
    column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 24) });

    var modeLabel = new Label { Text = "Режим игры", HorizontalAlignment = HorizontalAlignment.Center };
    modeLabel.AddThemeFontSizeOverride("font_size", LabelFontSize);
    column.AddChild(modeLabel);

    _mode = new OptionButton { Name = "Mode", FocusMode = FocusModeEnum.None };
    _mode.AddThemeFontSizeOverride("font_size", LabelFontSize);
    _mode.AddItem("Заработок денег", (int)GameMode.Money);
    _mode.AddItem("Развитие теорий", (int)GameMode.TheoryDevelopment);
    _mode.Selected = _mode.GetItemIndex((int)GameMode.TheoryDevelopment);
    column.AddChild(_mode);

    _failureConsequences = new CheckButton
    {
      Name = "FailureConsequences",
      Text = "Провал: бездомный уходит, гражданин понижается",
      ButtonPressed = true,
      FocusMode = FocusModeEnum.None,
    };
    _failureConsequences.AddThemeFontSizeOverride("font_size", LabelFontSize);
    column.AddChild(_failureConsequences);

    _playersLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
    _playersLabel.AddThemeFontSizeOverride("font_size", LabelFontSize);
    column.AddChild(_playersLabel);

    _players = new HSlider
    {
      Name = "Players",
      MinValue = MinPlayers,
      MaxValue = FieldLayout.MaxPlayers,
      Step = 1,
      Value = DefaultPlayers,
      TickCount = FieldLayout.MaxPlayers - MinPlayers + 1,
      TicksOnBorders = true,
      FocusMode = FocusModeEnum.None,
    };
    _players.ValueChanged += _ => UpdatePlayersLabel();
    column.AddChild(_players);
    UpdatePlayersLabel();

    column.AddChild(CreateButton("Network", "Сетевая игра", ShowNetworkNotice));
    var localRow = new HBoxContainer();
    localRow.AddThemeConstantOverride("separation", 12);
    column.AddChild(localRow);
    localRow.AddChild(CreateButton("Local", "Локальная игра", () => StartLocalGame()));
    localRow.AddChild(CreateButton("Dev", "Режим разработчика", () => StartLocalGame(devMode: true)));
    column.AddChild(CreateButton("Quit", "Выйти", () => GetTree().Quit()));
  }

  /// <summary>
  /// Сетевая игра пока не реализована: сообщаем об этом.
  /// </summary>
  private void ShowNetworkNotice()
  {
    var notice = new AcceptDialog { DialogText = "Сетевая игра пока не реализована." };
    AddChild(notice);
    notice.PopupCentered();
  }

  private int PlayerCount => (int)_players.Value;

  private void UpdatePlayersLabel() => _playersLabel.Text = $"Количество игроков: {PlayerCount}";

  private static Button CreateButton(string name, string text, Action pressed)
  {
    var button = new Button
    {
      Name = name,
      Text = text,
      CustomMinimumSize = new Vector2(0, ButtonHeight),
      FocusMode = FocusModeEnum.None,
    };
    button.AddThemeFontSizeOverride("font_size", ButtonFontSize);
    button.Pressed += pressed;
    return button;
  }

  /// <summary>
  /// Локальная игра: каждый игрок по очереди вводит имя, затем начинается партия.
  /// </summary>
  /// <param name="devMode">Запустить с режимом разработчика.</param>
  private async void StartLocalGame(bool devMode = false)
  {
    var count = PlayerCount;
    var mode = SelectedMode;
    var consequences = _failureConsequences.ButtonPressed;
    var names = new List<string>();
    for (var number = 1; number <= count; number++)
      names.Add(await NameDialog.Ask(this, number));

    StartGame(names, devMode, mode, consequences);
  }

  /// <summary>
  /// Заменить меню партией с выбранным количеством игроков, их именами и режимом.
  /// </summary>
  private void StartGame(IReadOnlyList<string> names, bool devMode, GameMode mode, bool failureConsequences)
  {
    var tree = GetTree();
    var game = new Main
    {
      Name = "Main",
      PlayerCount = names.Count,
      PlayerNames = names,
      DevMode = devMode,
      Mode = mode,
      FailureConsequences = failureConsequences,
    };
    tree.Root.AddChild(game);
    tree.CurrentScene = game;
    QueueFree();
  }
}
