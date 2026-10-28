using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Cards;

/// <summary>
/// Карточка теории. Конкретные теории — наследники этого класса.
/// </summary>
public abstract class TheoryCard : Card
{
  /// <summary>
  /// Теория, к которой относится карточка. У карточки теории она обязательна.
  /// </summary>
  public new Theory Theory => base.Theory!;

  protected TheoryCard(string name, Theory theory, Effect onPlayFromHand, PassiveAbility? passive = null)
    : base(name, theory ?? throw new ArgumentNullException(nameof(theory)), onPlayFromHand, passive)
  {
  }
}
