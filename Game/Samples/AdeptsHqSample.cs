using ConspiracyLeague.Game.Cards;
using Godot;

namespace ConspiracyLeague.Game.Samples;

/// <summary>
/// Витрина бродящих адептов у штаба из трёх карточек и разбегания от кликов: один клик, второй, потом пять подряд —
/// с третьего подряд адепты бегут всё быстрее. Место клика отмечается кружком.
/// </summary>
public partial class AdeptsHqSample : Node2D
{
  private const float StackStep = 60;

  private static readonly Color TableColor = new("26342c");
  private static readonly Color CardColor = new("efe6d2");
  private static readonly Color CardEdgeColor = new("8a6d3b");
  private static readonly Color ClickColor = new(1, 0.85f, 0.2f);

  private static readonly double[] Clicks = [2.0, 4.6, 7.0, 7.3, 7.6, 7.9, 8.2];

  private AdeptsView _adepts = null!;
  private double _time;
  private int _next;
  private Vector2 _click;
  private double _clickAt = double.NegativeInfinity;

  private static Rect2[] Stack =>
    Enumerable.Range(0, 3).Select(i => new Rect2(-CardPainter.CardSize / 2 - new Vector2(0, i * StackStep), CardPainter.CardSize)).ToArray();

  public override void _Ready()
  {
    Scale *= 1.6f;
    var stack = Stack;
    var home = stack.Aggregate((a, b) => a.Merge(b));
    _adepts = new AdeptsView
    {
      Position = new Vector2(360, 270), Look = MeepleView.MeepleLook.Suit, Color = new Color("2c3038"),
      GroundsSource = () => new AdeptsView.Grounds(home, stack),
    };
    AddChild(_adepts);
    _adepts.Add(14);
  }

  public override void _Process(double delta)
  {
    _time += delta;
    QueueRedraw();
    if (_next >= Clicks.Length || _time < Clicks[_next])
      return;

    _next++;
    // Клик в самую гущу: рядом с адептом, у которого больше всего соседей.
    var meeples = _adepts.GetChildren().OfType<WalkingAdeptView>().ToList();
    var meeple = meeples.MaxBy(view => meeples.Count(other => other.Position.DistanceTo(view.Position) < 40));
    _click = meeple is null ? Vector2.Zero : meeple.Position + new Vector2(8, 3);
    _clickAt = _time;
    _adepts.Scare(_click);
  }

  public override void _Draw()
  {
    DrawRect(new Rect2(Vector2.Zero, AdeptWalkSamples.StageSize), TableColor);
    foreach (var card in Stack.Reverse())
    {
      var rect = new Rect2(card.Position + _adepts.Position, card.Size);
      DrawRect(rect, CardColor);
      DrawRect(rect, CardEdgeColor, filled: false, width: 1.5f);
    }

    var age = (float)(_time - _clickAt);
    if (age < 0.5f)
      DrawArc(_adepts.Position + _click, 4 + age * 30, 0, Mathf.Tau, 24, ClickColor with { A = 1 - age * 2 }, 1.5f);
    var font = ThemeDB.FallbackFont;
    DrawString(font, new Vector2(10, 18), $"Кликов подряд: {(_next == 0 ? 0 : _next <= 2 ? 1 : _next - 1)}", fontSize: 10, modulate: Colors.White);
  }
}
