using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;

namespace ConspiracyLeague.Core.Cards.Professionals;

/// <summary>
/// Профессионал. Конкретные профессии (политик, учёный и т.д.) — наследники этого класса.
/// </summary>
/// <remarks>
/// Пример наследника:
/// <code>
/// public sealed class PoliticianCard : ProfessionalCard
/// {
///   public PoliticianCard()
///     : base("Политик", successChancePercent: 60, cost: 6,
///       onPlayFromHand: ..., onPlacedInHeadquarters: ..., passive: ...)
///   {
///   }
/// }
/// </code>
/// </remarks>
public abstract class ProfessionalCard : PersonCard
{
  protected ProfessionalCard(
    string name,
    int successChancePercent,
    int cost,
    Effect onPlayFromHand,
    Effect onPlacedInHeadquarters,
    PassiveAbility? passive = null)
    : base(name, successChancePercent, cost, onPlayFromHand, onPlacedInHeadquarters, passive)
  {
  }
}
