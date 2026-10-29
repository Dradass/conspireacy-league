using ConspiracyLeague.Core.Game;
using Godot;

namespace ConspiracyLeague.Game.Cards;

/// <summary>
/// Проверка успеха на карточке: слева от карточки вертикальная шкала до <see cref="SuccessCheck.MaxRoll"/>
/// с риской «успех» на уровне успешности персонажа. Шкала наполняется снизу вверх до результата проверки,
/// затем на карточке появляется зелёная галка (успех) или красный крест (провал).
/// </summary>
/// <remarks>
/// Узел добавляется дочерним к <see cref="CardView"/>: начало координат — центр карточки.
/// </remarks>
public partial class SuccessCheckView : Node2D
{
  private const double FillDuration = 1.0;
  private const float BarWidth = 16f;
  private const float BarGap = 6f;
  private const float TickOverhang = 6f;
  private const int LabelFontSize = 13;
  private const int ValueFontSize = 15;
  private const float MarkRadius = 34f;
  private const float MarkWidth = 9f;

  private static readonly Color TrackColor = new(0, 0, 0, 0.55f);
  private static readonly Color TrackBorderColor = new("fdfaf2");
  private static readonly Color FillColor = new("e8d9a8");
  private static readonly Color SuccessColor = new("3fa34d");
  private static readonly Color FailureColor = new("d1423a");
  private static readonly Color TickColor = new("fdfaf2");
  private static readonly Color LabelBackColor = new(0, 0, 0, 0.7f);
  private static readonly Color MarkBackColor = new(1, 1, 1, 0.9f);

  private SuccessCheck _check = null!;
  private float _value;
  private bool _finished;

  /// <summary>
  /// Создать проверку успеха на карточке. Анимация запускается <see cref="Play"/>.
  /// </summary>
  /// <param name="card">Карточка.</param>
  /// <param name="check">Проверка успеха.</param>
  public static SuccessCheckView Create(CardView card, SuccessCheck check)
  {
    // Шкала заходит на соседнюю ячейку — рисуем её поверх соседних карточек.
    var view = new SuccessCheckView { Name = "SuccessCheck", _check = check, ZIndex = 10 };
    card.AddChild(view);
    return view;
  }

  /// <summary>
  /// Наполнить шкалу до результата проверки и поставить на карточку галку или крест.
  /// </summary>
  /// <returns>Задача, которая завершается, когда появилась галка или крест.</returns>
  public async Task Play()
  {
    var tween = CreateTween();
    tween.TweenMethod(Callable.From<float>(SetValue), 0f, (float)_check.Roll, FillDuration)
      .SetTrans(Tween.TransitionType.Cubic)
      .SetEase(Tween.EaseType.Out);
    await ToSignal(tween, Tween.SignalName.Finished);

    _finished = true;
    QueueRedraw();
  }

  public override void _Draw()
  {
    var card = new Rect2(-CardPainter.CardSize / 2, CardPainter.CardSize);
    var bar = new Rect2(card.Position.X - BarGap - BarWidth, card.Position.Y, BarWidth, card.Size.Y);

    DrawRect(bar, TrackColor);
    var fillHeight = bar.Size.Y * _value / SuccessCheck.MaxRoll;
    var fillColor = !_finished ? FillColor : _check.Succeeded ? SuccessColor : FailureColor;
    DrawRect(new Rect2(bar.Position.X, bar.End.Y - fillHeight, bar.Size.X, fillHeight), fillColor);
    DrawRect(bar, TrackBorderColor, filled: false, width: 2);

    DrawTick(bar);
    DrawValue(bar, fillHeight);

    if (_finished)
      DrawMark(_check.Succeeded);
  }

  /// <summary>
  /// Риска «успех» на уровне успешности персонажа, подпись — слева от шкалы.
  /// </summary>
  private void DrawTick(Rect2 bar)
  {
    var y = bar.End.Y - bar.Size.Y * _check.Chance / SuccessCheck.MaxRoll;
    DrawLine(new Vector2(bar.Position.X - TickOverhang, y), new Vector2(bar.End.X + TickOverhang, y), TickColor, 3);

    var text = $"успех {_check.Chance}";
    var font = ThemeDB.FallbackFont;
    var size = font.GetStringSize(text, HorizontalAlignment.Left, -1, LabelFontSize) + new Vector2(8, 2);
    var label = new Rect2(bar.Position.X - TickOverhang - size.X, y - size.Y / 2, size);
    DrawRect(label, LabelBackColor);
    CardPainter.DrawCenteredText(this, text, label, LabelFontSize, TickColor);
  }

  /// <summary>
  /// Текущее значение шкалы — над её заполненной частью.
  /// </summary>
  private void DrawValue(Rect2 bar, float fillHeight)
  {
    var text = Mathf.RoundToInt(_value).ToString();
    var area = new Rect2(bar.Position.X - BarWidth, bar.End.Y - fillHeight - ValueFontSize - 4, bar.Size.X + 2 * BarWidth, ValueFontSize + 2);
    DrawRect(area, LabelBackColor);
    CardPainter.DrawCenteredText(this, text, area, ValueFontSize, TickColor);
  }

  /// <summary>
  /// Зелёная галка или красный крест в центре карточки.
  /// </summary>
  private void DrawMark(bool success)
  {
    DrawCircle(Vector2.Zero, MarkRadius, MarkBackColor);
    var r = MarkRadius * 0.55f;
    if (success)
    {
      DrawPolyline([new Vector2(-r, 0), new Vector2(-r * 0.3f, r * 0.7f), new Vector2(r, -r * 0.6f)], SuccessColor, MarkWidth, antialiased: true);
      return;
    }

    DrawLine(new Vector2(-r, -r), new Vector2(r, r), FailureColor, MarkWidth, antialiased: true);
    DrawLine(new Vector2(-r, r), new Vector2(r, -r), FailureColor, MarkWidth, antialiased: true);
  }

  private void SetValue(float value)
  {
    _value = value;
    QueueRedraw();
  }
}
