using ConspiracyLeague.Core.Cards;
using Godot;

namespace ConspiracyLeague.Game.Cards;

/// <summary>
/// Карточка на столе. Начало координат узла — центр карточки.
/// </summary>
public partial class CardView : Node2D
{
  private static readonly Vector2 LiftedScale = Vector2.One * 1.08f;

  private bool _lifted;
  private bool _isHeadquarters;

  /// <summary>
  /// Отображаемая карточка.
  /// </summary>
  public Card Card { get; private set; } = null!;

  /// <summary>
  /// Карточку держат в руке: она чуть крупнее и отбрасывает тень.
  /// </summary>
  public bool Lifted
  {
    get => _lifted;
    set
    {
      _lifted = value;
      Scale = value ? LiftedScale : Vector2.One;
      QueueRedraw();
    }
  }

  /// <summary>
  /// Карточка — штаб теории.
  /// </summary>
  public bool IsHeadquarters
  {
    get => _isHeadquarters;
    set
    {
      _isHeadquarters = value;
      QueueRedraw();
    }
  }

  /// <summary>
  /// Создать отображение карточки.
  /// </summary>
  public static CardView Create(Card card) => new() { Card = card, Name = card.GetType().Name };

  public override void _Draw() =>
    CardPainter.DrawFace(this, new Rect2(-CardPainter.CardSize / 2, CardPainter.CardSize), Card, _lifted, _isHeadquarters);
}
