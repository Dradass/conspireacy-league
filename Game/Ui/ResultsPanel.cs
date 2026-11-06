using ConspiracyLeague.Core.Game;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Итоги партии: победитель и игроки по убыванию (денег или победных очков — в зависимости от режима).
/// Нажатие на игрока раскрывает, за что он получил деньги или из чего складываются его очки.
/// Список прокручивается колесиком. Внизу кнопки «В меню» и «Выйти».
/// Занимает весь экран и затемняет стол.
/// </summary>
public partial class ResultsPanel : Control
{
  private const string StartMenuScene = "res://Game/StartMenu.tscn";
  private const float PanelWidth = 460f;
  private const float ListHeight = 420f;
  private const int TitleFontSize = 40;
  private const int RowFontSize = 28;
  private const int SourceFontSize = 18;
  private const int ButtonFontSize = 24;
  private const float ButtonHeight = 52f;
  private const int DetailsIndent = 24;

  private static readonly Color ShadeColor = new(0f, 0f, 0f, 0.6f);
  private static readonly Color PanelColor = new(0.08f, 0.1f, 0.09f, 0.95f);
  private static readonly Color TitleColor = new("e0b84f");
  private static readonly Color HintColor = new(1f, 1f, 1f, 0.6f);

  /// <summary>
  /// Сессия игры: итоговый рейтинг игроков.
  /// </summary>
  public GameSession Session { get; init; } = null!;

  public override void _Ready()
  {
    SetAnchorsPreset(LayoutPreset.FullRect);
    AddChild(new ColorRect { Color = ShadeColor, AnchorRight = 1, AnchorBottom = 1 });

    var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
    AddChild(center);

    var style = new StyleBoxFlat { BgColor = PanelColor };
    style.SetCornerRadiusAll(12);
    style.SetContentMarginAll(28);
    var panel = new PanelContainer { CustomMinimumSize = new Vector2(PanelWidth, 0) };
    panel.AddThemeStyleboxOverride("panel", style);
    center.AddChild(panel);

    var column = new VBoxContainer();
    column.AddThemeConstantOverride("separation", 14);
    panel.AddChild(column);

    var ranking = Session.Ranking;
    var title = new Label { Text = $"Победил {ranking[0].Name}!", HorizontalAlignment = HorizontalAlignment.Center };
    title.AddThemeFontSizeOverride("font_size", TitleFontSize);
    title.AddThemeColorOverride("font_color", TitleColor);
    column.AddChild(title);

    var hint = new Label { Text = "Нажмите на игрока, чтобы увидеть подробности", HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
    hint.AddThemeFontSizeOverride("font_size", SourceFontSize);
    hint.AddThemeColorOverride("font_color", HintColor);
    column.AddChild(hint);

    var scroll = new ScrollContainer
    {
      Name = "Players",
      CustomMinimumSize = new Vector2(0, ListHeight),
      HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
      VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
      SizeFlagsVertical = Control.SizeFlags.ExpandFill,
    };
    column.AddChild(scroll);

    var rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
    rows.AddThemeConstantOverride("separation", 8);
    scroll.AddChild(rows);

    for (var i = 0; i < ranking.Count; i++)
    {
      var player = ranking[i];
      var details = Session.Mode == GameMode.Money ? CreateEarningsDetails(player) : CreateScoreDetails(player);
      var indent = new MarginContainer { Visible = false };
      indent.AddThemeConstantOverride("margin_left", DetailsIndent);
      indent.AddChild(details);

      var row = CreateRowButton(i + 1, player);
      row.Pressed += () =>
      {
        indent.Visible = !indent.Visible;
        row.Text = RowText(i + 1, player, indent.Visible);
      };

      rows.AddChild(row);
      rows.AddChild(indent);
    }

    column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
    column.AddChild(CreateButton("Menu", "В меню", () => GetTree().ChangeSceneToFile(StartMenuScene)));
    column.AddChild(CreateButton("Quit", "Выйти", () => GetTree().Quit()));
  }

  /// <summary>
  /// Строка игрока: в режиме денег — деньги, в режиме развития теорий — победные очки (и деньги в скобках).
  /// </summary>
  private string RowText(int place, Player player, bool expanded)
  {
    var value = Session.Mode == GameMode.Money
      ? $"{player.Money}$"
      : $"{Session.ScoreOf(player).Total} ПО ({player.Money}$)";
    return $"{(expanded ? "[-]" : "[+]")} {place}. {player.Name} — {value}";
  }

  /// <summary>
  /// Строка игрока: нажатие раскрывает или сворачивает подробности.
  /// </summary>
  private Button CreateRowButton(int place, Player player)
  {
    var button = new Button
    {
      Text = RowText(place, player, expanded: false),
      Alignment = HorizontalAlignment.Left,
      Flat = true,
      FocusMode = FocusModeEnum.None,
      SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
    };
    button.AddThemeFontSizeOverride("font_size", RowFontSize);
    button.AddThemeColorOverride("font_color", PlayerPalette.MainOf(player.Color));
    return button;
  }

  /// <summary>
  /// Режим денег: за что игрок получил деньги, от большего к меньшему.
  /// </summary>
  private static VBoxContainer CreateEarningsDetails(Player player)
  {
    var details = new VBoxContainer();
    details.AddThemeConstantOverride("separation", 2);

    if (player.EarnedFrom.Count == 0)
    {
      details.AddChild(CreateSourceLabel("Денег не получал."));
      return details;
    }

    foreach (var (source, amount) in player.EarnedFrom.OrderByDescending(entry => entry.Value))
      details.AddChild(CreateSourceLabel($"{source}: +{amount}$"));
    return details;
  }

  /// <summary>
  /// Режим развития теорий: из чего складываются победные очки игрока.
  /// </summary>
  private VBoxContainer CreateScoreDetails(Player player)
  {
    var score = Session.ScoreOf(player);
    var details = new VBoxContainer();
    details.AddThemeConstantOverride("separation", 2);
    details.AddChild(CreateSourceLabel($"Влияние лидеров × {VictoryPoints.LeaderPointsPerInfluence}: +{score.LeaderPoints} ПО"));
    details.AddChild(CreateSourceLabel($"Адепты: +{score.AdeptPoints} ПО"));
    details.AddChild(CreateSourceLabel($"Теорий ({score.TheoryCount}): +{score.TheoryPoints} ПО"));
    details.AddChild(CreateSourceLabel($"Деньги {player.Money}$ (по 1 ПО за {VictoryPoints.MoneyPerPoint}$): +{score.MoneyPoints} ПО"));
    return details;
  }

  private static Label CreateSourceLabel(string text)
  {
    var line = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
    line.AddThemeFontSizeOverride("font_size", SourceFontSize);
    return line;
  }

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
}
