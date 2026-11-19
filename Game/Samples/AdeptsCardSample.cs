using ConspiracyLeague.Game.Cards;
using Godot;

namespace ConspiracyLeague.Game.Samples;

/// <summary>
/// Витрина <see cref="AdeptsView"/>: на карточки приходят адепты, потом часть уходит, остальные подходят плотнее.
/// </summary>
public partial class AdeptsCardSample : Node2D
{
  private static readonly Color TableColor = new("26342c");
  private static readonly Color CardColor = new("efe6d2");

  private static readonly (MeepleView.MeepleLook Look, Color Color)[] Styles =
  [
    (MeepleView.MeepleLook.Wooden, MeepleView.Gray),
    (MeepleView.MeepleLook.Suit, new Color("2c3038")),
    (MeepleView.MeepleLook.LabCoat, new Color("f2f2f2")),
  ];

  private readonly List<AdeptsView> _views = [];
  private double _time;
  private int _step;

  public override void _Ready()
  {
    // Карточки крупнее, чем на столе.
    Scale *= 2;
    for (var i = 0; i < Styles.Length; i++)
    {
      var view = new AdeptsView { Look = Styles[i].Look, Color = Styles[i].Color, Position = new Vector2(100 + i * 180, 160) };
      _views.Add(view);
      AddChild(view);
    }
  }

  public override void _Process(double delta)
  {
    _time += delta;
    (double At, Action<AdeptsView> Do)[] script =
    [
      (0.2, view => view.Add(3)),
      (1.4, view => view.Add(4)),
      (2.8, view => view.ShrinkTo(4)),
      (4.0, view => view.Add(2)),
    ];
    while (_step < script.Length && _time >= script[_step].At)
      _views.ForEach(script[_step++].Do);
  }

  public override void _Draw()
  {
    DrawRect(new Rect2(Vector2.Zero, AdeptWalkSamples.StageSize), TableColor);
    foreach (var view in _views)
      DrawRect(new Rect2(view.Position - CardPainter.CardSize / 2, CardPainter.CardSize), CardColor);
  }
}
