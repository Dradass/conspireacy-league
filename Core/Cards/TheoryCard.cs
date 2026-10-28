using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;

namespace ConspiracyLeague.Core.Cards;

/// <summary>
/// Карточка теории. Конкретные теории — наследники этого класса.
/// </summary>
public abstract class TheoryCard : Card
{
  protected TheoryCard(string name, Effect onPlayFromHand, PassiveAbility? passive = null)
    : base(name, onPlayFromHand, passive)
  {
  }
}
