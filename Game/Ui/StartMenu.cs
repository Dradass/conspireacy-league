using ConspiracyLeague.Core.Game;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Стартовое меню: режим игры, начало партии (игроки и их имена — в отдельном окне, <see cref="PlayersDialog"/>),
/// опции (<see cref="OptionsDialog"/>) и выход из игры.
/// </summary>
public partial class StartMenu : Control
{
  private const float MenuWidth = 420f;
  private const int TitleFontSize = 48;
  private const int LabelFontSize = 24;
  private const int ButtonFontSize = 28;
  private const float ButtonHeight = 60f;

  /// <summary>
  /// Слой фонарика: выше окон меню, чтобы свет падал и на них.
  /// </summary>
  private const int FlashlightLayer = 10;

  private OptionButton _mode = null!;

  /// <summary>
  /// Выбранный режим партии.
  /// </summary>
  private GameMode SelectedMode => (GameMode)_mode.GetSelectedId();

  public override void _Ready()
  {
    UiStyle.Apply();
    SetAnchorsPreset(LayoutPreset.FullRect);
    AddChild(new MenuBackdrop { Name = "Backdrop" });

    var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
    AddChild(center);

    var column = new VBoxContainer { CustomMinimumSize = new Vector2(MenuWidth, 0) };
    column.AddThemeConstantOverride("separation", 18);
    center.AddChild(column);

    var title = new Label { Text = "Conspiracy League", HorizontalAlignment = HorizontalAlignment.Center };
    title.AddThemeFontSizeOverride("font_size", TitleFontSize);
    UiStyle.MakeTitle(title, UiStyle.MenuTitle);
    column.AddChild(title);
    column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 24) });

    var modeLabel = new Label { Text = "Режим игры", HorizontalAlignment = HorizontalAlignment.Center };
    modeLabel.AddThemeFontSizeOverride("font_size", LabelFontSize);
    UiStyle.MakeTitle(modeLabel, UiStyle.MenuTitle);
    column.AddChild(modeLabel);

    _mode = new OptionButton { Name = "Mode", FocusMode = FocusModeEnum.None };
    _mode.AddThemeFontSizeOverride("font_size", LabelFontSize);
    _mode.AddItem("Заработок денег", (int)GameMode.Money);
    _mode.AddItem("Развитие теорий", (int)GameMode.TheoryDevelopment);
    _mode.AddItem("Гонка", (int)GameMode.Race);
    _mode.Selected = _mode.GetItemIndex((int)GameMode.TheoryDevelopment);
    column.AddChild(_mode);

    column.AddChild(CreateButton("Network", "Сетевая игра", ShowNetworkNotice));
    var localRow = new HBoxContainer();
    localRow.AddThemeConstantOverride("separation", 12);
    column.AddChild(localRow);
    localRow.AddChild(CreateButton("Local", "Локальная игра", () => StartLocalGame()));
    localRow.AddChild(CreateButton("Dev", "Режим разработчика", () => StartLocalGame(devMode: true)));
    column.AddChild(CreateButton("Options", "Опции", () => OptionsDialog.Open(this)));
    column.AddChild(CreateButton("Quit", "Выйти", () => GetTree().Quit()));

    // Фонарик в отдельном слое поверх меню и его окон: курсор подсвечивает круг вокруг себя.
    var light = new CanvasLayer { Name = "FlashlightLayer", Layer = FlashlightLayer };
    light.AddChild(new Flashlight { Name = "Flashlight" });
    AddChild(light);
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

  private static Button CreateButton(string name, string text, Action pressed)
  {
    var button = new Button
    {
      Name = name,
      Text = text,
      CustomMinimumSize = new Vector2(0, ButtonHeight),
      SizeFlagsHorizontal = SizeFlags.ExpandFill,
      FocusMode = FocusModeEnum.None,
    };
    button.AddThemeFontSizeOverride("font_size", ButtonFontSize);
    button.Pressed += pressed;
    return button;
  }

  /// <summary>
  /// Локальная игра: в окне выбирают количество игроков и вводят их имена, затем начинается партия.
  /// «Назад» возвращает в меню.
  /// </summary>
  /// <param name="devMode">Запустить с режимом разработчика.</param>
  private async void StartLocalGame(bool devMode = false)
  {
    var mode = SelectedMode;
    var names = await PlayersDialog.Ask(this, devMode ? "Режим разработчика" : "Локальная игра");
    if (names is null)
      return;

    StartGame(names, devMode, mode);
  }

  /// <summary>
  /// Заменить меню партией с выбранными игроками, режимом и опциями (<see cref="GameOptions"/>).
  /// </summary>
  private void StartGame(IReadOnlyList<string> names, bool devMode, GameMode mode)
  {
    var tree = GetTree();
    var game = new Main
    {
      Name = "Main",
      PlayerCount = names.Count,
      PlayerNames = names,
      DevMode = devMode,
      Mode = mode,
      FailureConsequences = GameOptions.CriticalFailure,
      RadialField = GameOptions.RadialField,
    };
    tree.Root.AddChild(game);
    tree.CurrentScene = game;
    QueueFree();
  }
}
