using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Game.Table;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Сводка дохода от адептов в начале раунда: каждая строка — теория, которая принесла деньги. Сначала общий доход
/// адептов теории, затем сколько заработал каждый игрок. Закрывается кнопкой «Закрыть» или нажатием вне окна.
/// </summary>
public partial class IncomeSummaryPanel : Control
{
  private const int TextWidth = 720;
  private const int TextFontSize = 20;
  private const int TitleFontSize = 26;
  private static readonly Color NeutralTheoryColor = new("d8e6dc");
  private static readonly Color PanelColor = new(0.08f, 0.1f, 0.09f, 0.95f);

  private ColorRect _dim = null!;
  private CenterContainer _center = null!;
  private RichTextLabel _text = null!;

  public override void _Ready()
  {
    MouseFilter = MouseFilterEnum.Stop;
    Visible = false;

    _dim = new ColorRect { Color = new Color(0, 0, 0, 0.5f), MouseFilter = MouseFilterEnum.Ignore };
    AddChild(_dim);

    _center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
    AddChild(_center);

    var style = new StyleBoxFlat { BgColor = PanelColor };
    style.SetCornerRadiusAll(12);
    style.SetContentMarginAll(24);
    var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
    panel.AddThemeStyleboxOverride("panel", style);
    _center.AddChild(panel);

    var column = new VBoxContainer();
    column.AddThemeConstantOverride("separation", 12);
    panel.AddChild(column);

    var title = new Label { Text = "Итоги конца раунда" };
    title.AddThemeFontSizeOverride("font_size", TitleFontSize);
    column.AddChild(title);

    _text = new RichTextLabel
    {
      BbcodeEnabled = true,
      FitContent = true,
      CustomMinimumSize = new Vector2(TextWidth, 0),
    };
    _text.AddThemeFontSizeOverride("normal_font_size", TextFontSize);
    column.AddChild(_text);

    var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
    column.AddChild(buttons);
    var close = new Button { Text = "Закрыть", FocusMode = FocusModeEnum.None };
    close.Pressed += Close;
    buttons.AddChild(close);
  }

  public override void _Process(double delta)
  {
    if (Visible)
      Layout();
  }

  public override void _GuiInput(InputEvent @event)
  {
    // Нажатие левой или правой кнопкой вне окна (фон) закрывает сводку. Колесо не закрывает.
    if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left or MouseButton.Right })
    {
      AcceptEvent();
      Close();
    }
  }

  /// <summary>
  /// Показать сводку. Строка — теория: сумма дохода, затем доход каждого игрока.
  /// </summary>
  /// <param name="incomes">Доход по теориям за конец раунда.</param>
  public void Open(IReadOnlyList<TheoryIncome> incomes, SolvencyChanges solvency)
  {
    var lines = incomes.Select(FormatLine).ToList();
    if (incomes.Count == 0)
      lines.Add("Адепты денег не принесли.");
    if (solvency.Closed.Count > 0 || solvency.Reopened.Count > 0)
      lines.Add("");
    if (solvency.Closed.Count > 0)
      lines.Add($"[b]Закрылись:[/b] {string.Join(", ", solvency.Closed.Select(column => $"[color=#e06060]{column.Theory}[/color]"))}");
    if (solvency.Reopened.Count > 0)
      lines.Add($"[b]Открылись:[/b] {string.Join(", ", solvency.Reopened.Select(column => $"[color=#60e080]{column.Theory}[/color]"))}");
    _text.Text = string.Join("\n", lines);
    Layout();
    Visible = true;
  }

  private static string FormatLine(TheoryIncome income)
  {
    // Имя игрока — цветом его фишки, как на столе.
    var players = string.Join(", ", income.ByPlayer.Select(entry =>
      $"[color={PlayerPalette.MainOf(entry.Player.Color).ToHtml()}]{entry.Player.Name}[/color] — {entry.Amount}$"));
    // Название теории — цветом её подсветки на общем поле, полупрозрачным. Теориям без цвета — нейтральный.
    var color = (TheoryHighlights.ColorOf(income.Column.Theory) ?? NeutralTheoryColor) with { A = 0.5f };
    return $"[b][color={color.ToHtml()}]{income.Column.Theory}[/color][/b]: {income.Total}$ ({players})";
  }

  private void Close() => Visible = false;

  /// <summary>
  /// Разместить затемнение и центрированное окно по размеру экрана (без якорей).
  /// </summary>
  private void Layout()
  {
    var screen = GetViewportRect().Size;
    Position = Vector2.Zero;
    Size = screen;
    _dim.Position = Vector2.Zero;
    _dim.Size = screen;
    _center.Position = Vector2.Zero;
    _center.Size = screen;
  }
}
