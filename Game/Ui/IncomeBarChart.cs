using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Game.Table;
using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Столбчатая диаграмма дохода от адептов за конец раунда: по оси X — теории, которые принесли деньги, столбец
/// каждой теории сложен из столбиков игроков (цветом игрока) высотой в полученные им $. Над столбцом — весь доход
/// теории, под осью — названия теорий, над диаграммой — легенда игроков.
/// </summary>
public partial class IncomeBarChart : Control
{
  private const float ChartHeight = 300;
  private const float LegendHeight = 34;
  private const float AxisWidth = 44;
  private const float LabelAreaHeight = 130;
  private const float TopPadding = 26;
  private const float MaxBarWidth = 64;
  private const float BarGapShare = 0.35f;
  private const float LabelAngle = -0.6f;
  private const int FontSize = 16;
  private const int SmallFontSize = 14;
  private const int GridLines = 5;
  private const float NameLightening = 0.5f;

  private static readonly Color AxisColor = new("b8c7bd");
  private static readonly Color GridColor = new(1, 1, 1, 0.08f);
  private static readonly Color SegmentBorder = new(0, 0, 0, 0.45f);

  private IReadOnlyList<TheoryIncome> _incomes = [];
  private IReadOnlyList<Player> _players = [];

  /// <summary>
  /// Показать доход по теориям. Теории без дохода не показываются.
  /// </summary>
  public void Show(IReadOnlyList<TheoryIncome> incomes)
  {
    _incomes = incomes.Where(income => income.Total > 0).ToList();
    // Игроки для легенды — в порядке первого появления в столбцах.
    _players = _incomes.SelectMany(income => income.ByPlayer.Where(entry => entry.Amount > 0).Select(entry => entry.Player))
      .Distinct()
      .ToList();
    CustomMinimumSize = new Vector2(CustomMinimumSize.X, LegendHeight + TopPadding + ChartHeight + LabelAreaHeight);
    QueueRedraw();
  }

  public override void _Draw()
  {
    if (_incomes.Count == 0)
      return;

    var font = UiStyle.TextFont;
    var numberFont = UiStyle.NumberFont;
    DrawLegend(font);

    var top = LegendHeight + TopPadding;
    var bottom = top + ChartHeight;
    var left = AxisWidth;
    var width = Size.X - left;
    var max = NiceMax(_incomes.Max(income => income.Total));
    var scale = ChartHeight / max;

    // Сетка и подписи оси Y.
    for (var line = 0; line <= GridLines; line++)
    {
      var value = max * line / GridLines;
      var y = bottom - value * scale;
      if (line > 0)
        DrawLine(new Vector2(left, y), new Vector2(Size.X, y), GridColor);
      var text = $"{value}$";
      var textWidth = numberFont.GetStringSize(text, fontSize: SmallFontSize).X;
      DrawString(numberFont, new Vector2(left - 6 - textWidth, y + SmallFontSize * 0.35f), text,
        fontSize: SmallFontSize, modulate: AxisColor);
    }

    // Столбцы.
    var slot = width / _incomes.Count;
    var barWidth = Math.Min(MaxBarWidth, slot * (1 - BarGapShare));
    for (var index = 0; index < _incomes.Count; index++)
    {
      var income = _incomes[index];
      var x = left + slot * index + (slot - barWidth) / 2;
      var y = bottom;
      foreach (var (player, amount) in income.ByPlayer.Where(entry => entry.Amount > 0))
      {
        var height = amount * scale;
        var rect = new Rect2(x, y - height, barWidth, height);
        DrawRect(rect, PlayerPalette.MainOf(player.Color));
        DrawRect(rect, SegmentBorder, filled: false, width: 1);
        // Сумма игрока — внутри столбика, если помещается.
        var amountText = amount.ToString();
        var amountSize = numberFont.GetStringSize(amountText, fontSize: SmallFontSize);
        if (height >= SmallFontSize + 2 && barWidth >= amountSize.X + 4)
          DrawString(numberFont, new Vector2(x + (barWidth - amountSize.X) / 2, y - height / 2 + SmallFontSize * 0.35f),
            amountText, fontSize: SmallFontSize, modulate: Colors.Black);
        y -= height;
      }

      // Весь доход теории — над столбцом.
      var totalText = $"{income.Total}$";
      var totalWidth = numberFont.GetStringSize(totalText, fontSize: FontSize).X;
      DrawString(numberFont, new Vector2(x + (barWidth - totalWidth) / 2, y - 6), totalText, fontSize: FontSize,
        modulate: UiStyle.Text);

      // Название теории — наклонно под осью, цветом её подсветки на поле, осветлённым, чтобы читалось на тёмном фоне.
      var name = income.Column.Theory.ToString();
      var color = TheoryHighlights.ColorOf(income.Column.Theory) is { } highlight ? highlight.Lightened(NameLightening) : UiStyle.Text;
      DrawSetTransform(new Vector2(x + barWidth / 2, bottom + 10), LabelAngle);
      var nameWidth = font.GetStringSize(name, fontSize: SmallFontSize).X;
      DrawString(font, new Vector2(-nameWidth, SmallFontSize), name, fontSize: SmallFontSize, modulate: color);
      DrawSetTransform(Vector2.Zero);
    }

    // Оси.
    DrawLine(new Vector2(left, top - 6), new Vector2(left, bottom), AxisColor, 2);
    DrawLine(new Vector2(left, bottom), new Vector2(Size.X, bottom), AxisColor, 2);
  }

  /// <summary>
  /// Легенда: цветной квадрат и имя каждого игрока, получившего доход.
  /// </summary>
  private void DrawLegend(Font font)
  {
    var x = AxisWidth;
    foreach (var player in _players)
    {
      DrawRect(new Rect2(x, 8, 16, 16), PlayerPalette.MainOf(player.Color));
      x += 22;
      DrawString(font, new Vector2(x, 22), player.Name, fontSize: FontSize, modulate: UiStyle.Text);
      x += font.GetStringSize(player.Name, fontSize: FontSize).X + 24;
    }
  }

  /// <summary>
  /// Верх оси Y — круглое число не меньше максимума, которое ровно делится на линии сетки.
  /// </summary>
  private static int NiceMax(int max)
  {
    foreach (var step in new[] { 1, 2, 5, 10, 20, 25, 50, 100, 200, 500 })
    {
      if (step * GridLines >= max)
        return step * GridLines;
    }

    return (max + GridLines - 1) / GridLines * GridLines;
  }
}
