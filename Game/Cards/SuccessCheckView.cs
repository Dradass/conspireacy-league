using ConspiracyLeague.Core.Game;
using Godot;

namespace ConspiracyLeague.Game.Cards;

/// <summary>
/// Проверка успеха на карточке: на карточку бросается двадцатигранный кубик. Он катится, кувыркаясь
/// и мелькая гранями, и останавливается на выпавшей грани. Под кубиком подписано, на каких гранях успех.
/// Затем кубик окрашивается, и на нём появляется зелёная галка (успех) или красный крест (провал).
/// </summary>
/// <remarks>
/// Узел добавляется дочерним к <see cref="CardView"/>: начало координат — центр карточки.
/// </remarks>
public partial class SuccessCheckView : Node2D
{
  private const double RollDuration = 1.0;
  private const float DieRadius = 34f;
  private const float Spin = 3 * Mathf.Tau;
  private const int FaceChanges = 14;
  private const int FaceFontSize = 22;
  private const int LabelFontSize = 13;
  private const float MarkRadius = 14f;
  private const float MarkWidth = 4.5f;

  private static readonly Vector2 ThrowFrom = new(-110, -70);

  private static readonly Color DieColor = new("2f3b57");
  private static readonly Color FacetColor = new("44547a");
  private static readonly Color EdgeColor = new("fdfaf2");
  private static readonly Color SuccessColor = new("3fa34d");
  private static readonly Color FailureColor = new("d1423a");
  private static readonly Color TextColor = new("fdfaf2");
  private static readonly Color LabelBackColor = new(0, 0, 0, 0.7f);
  private static readonly Color MarkBackColor = new(1, 1, 1, 0.95f);

  private SuccessCheck _check = null!;
  private Vector2 _position;
  private float _angle;
  private int _face;
  private int _faceChanges;
  private bool _finished;

  /// <summary>
  /// Создать проверку успеха на карточке. Анимация запускается <see cref="Play"/>.
  /// </summary>
  /// <param name="card">Карточка.</param>
  /// <param name="check">Проверка успеха.</param>
  public static SuccessCheckView Create(CardView card, SuccessCheck check)
  {
    // Кубик прилетает с соседней ячейки — рисуем его поверх соседних карточек.
    var view = new SuccessCheckView { Name = "SuccessCheck", _check = check, ZIndex = 10 };
    card.AddChild(view);
    return view;
  }

  /// <summary>
  /// Бросить кубик и поставить на нём галку или крест.
  /// </summary>
  /// <returns>Задача, которая завершается, когда кубик остановился и появилась галка или крест.</returns>
  public async Task Play()
  {
    SetProgress(0f);
    var tween = CreateTween();
    tween.TweenMethod(Callable.From<float>(SetProgress), 0f, 1f, RollDuration);
    await ToSignal(tween, Tween.SignalName.Finished);

    _finished = true;
    QueueRedraw();
  }

  public override void _Draw()
  {
    DrawLabel();

    DrawSetTransform(_position, _angle);
    var edgeColor = !_finished ? EdgeColor : _check.Succeeded ? SuccessColor : FailureColor;
    DrawDie(edgeColor);
    DrawSetTransform(_position);
    CardPainter.DrawCenteredText(this, _face.ToString(), FaceRect(), FaceFontSize, TextColor);

    if (_finished)
      DrawMark(new Vector2(DieRadius * 0.85f, -DieRadius * 0.85f), _check.Succeeded);
  }

  /// <summary>
  /// Двадцатигранник спереди: шестиугольный контур, в середине — треугольная грань, от неё к углам идут рёбра.
  /// </summary>
  private void DrawDie(Color edgeColor)
  {
    var hexagon = new Vector2[6];
    for (var i = 0; i < hexagon.Length; i++)
      hexagon[i] = Vector2.FromAngle(-Mathf.Pi / 2 + i * Mathf.Tau / 6) * DieRadius;
    Vector2[] face = [new(0, -DieRadius * 0.5f), new(DieRadius * 0.47f, DieRadius * 0.32f), new(-DieRadius * 0.47f, DieRadius * 0.32f)];

    DrawColoredPolygon(hexagon, DieColor);
    DrawColoredPolygon(face, FacetColor);

    var width = _finished ? 3f : 2f;
    // Рёбра от вершин центральной грани к углам шестиугольника.
    (int Face, int Corner)[] edges = [(0, 0), (0, 1), (0, 5), (1, 1), (1, 2), (1, 3), (2, 3), (2, 4), (2, 5)];
    foreach (var (faceVertex, corner) in edges)
      DrawLine(face[faceVertex], hexagon[corner], edgeColor, 1.5f, antialiased: true);
    DrawPolyline([.. face, face[0]], edgeColor, 1.5f, antialiased: true);
    DrawPolyline([.. hexagon, hexagon[0]], edgeColor, width, antialiased: true);
  }

  /// <summary>
  /// Где писать грань: в центральном треугольнике.
  /// </summary>
  private static Rect2 FaceRect() =>
    new(-DieRadius * 0.4f, -DieRadius * 0.2f, DieRadius * 0.8f, DieRadius * 0.45f);

  /// <summary>
  /// Подпись под карточкой, на каких гранях успех.
  /// </summary>
  private void DrawLabel()
  {
    var first = _check.FirstSuccessFace;
    var text = first <= 1 ? "успех на любой грани"
      : first > SuccessCheck.DieSides ? "провал на любой грани"
      : first == SuccessCheck.DieSides ? $"успех на {first}"
      : $"успех на {first}–{SuccessCheck.DieSides}";
    var font = ThemeDB.FallbackFont;
    var size = font.GetStringSize(text, HorizontalAlignment.Left, -1, LabelFontSize) + new Vector2(10, 4);
    var label = new Rect2(new Vector2(-size.X / 2, CardPainter.CardSize.Y / 2 - size.Y - 6), size);
    DrawRect(label, LabelBackColor);
    CardPainter.DrawCenteredText(this, text, label, LabelFontSize, TextColor);
  }

  /// <summary>
  /// Зелёная галка или красный крест на значке у кубика.
  /// </summary>
  /// <param name="center">Центр значка относительно центра кубика.</param>
  private void DrawMark(Vector2 center, bool success)
  {
    var color = success ? SuccessColor : FailureColor;
    DrawCircle(center, MarkRadius, MarkBackColor);
    DrawArc(center, MarkRadius, 0, Mathf.Tau, 24, color, 2, antialiased: true);
    var r = MarkRadius * 0.5f;
    if (success)
    {
      DrawPolyline([center + new Vector2(-r, 0), center + new Vector2(-r * 0.3f, r * 0.7f), center + new Vector2(r, -r * 0.6f)], color, MarkWidth, antialiased: true);
      return;
    }

    DrawLine(center + new Vector2(-r, -r), center + new Vector2(r, r), color, MarkWidth, antialiased: true);
    DrawLine(center + new Vector2(-r, r), center + new Vector2(r, -r), color, MarkWidth, antialiased: true);
  }

  /// <summary>
  /// Ход броска: кубик летит к центру карточки, замедляясь, крутится и мелькает гранями, всё реже.
  /// </summary>
  private void SetProgress(float progress)
  {
    var eased = 1 - Mathf.Pow(1 - progress, 3);
    _position = ThrowFrom.Lerp(Vector2.Zero, eased);
    _angle = (1 - eased) * Spin;

    var changes = Mathf.FloorToInt(eased * FaceChanges);
    if (progress >= 1f)
      _face = _check.Die;
    else if (changes != _faceChanges || _face == 0)
      _face = RandomFaceExcept(_face);
    _faceChanges = changes;
    QueueRedraw();
  }

  private static int RandomFaceExcept(int face)
  {
    var next = Random.Shared.Next(1, SuccessCheck.DieSides);
    return next >= face ? next + 1 : next;
  }
}
