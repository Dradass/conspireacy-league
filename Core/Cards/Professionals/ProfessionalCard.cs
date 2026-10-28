using ConspiracyLeague.Core.Effects;
using ConspiracyLeague.Core.Passives;
using ConspiracyLeague.Core.Theories;

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
  /// <summary>
  /// Влияние профессионала.
  /// </summary>
  public const int ProfessionalInfluence = 3;

  protected ProfessionalCard(
    string name,
    int successChancePercent,
    int cost,
    Effect onPlayFromHand,
    Effect onPlacedInHeadquarters,
    PassiveAbility? passive = null,
    Theory? theory = null)
    : base(name, successChancePercent, cost, ProfessionalInfluence, onPlayFromHand, onPlacedInHeadquarters, passive, theory)
  {
  }
}
