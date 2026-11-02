using ConspiracyLeague.Core.Cards.Professionals;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// При выкладывании в штаб теории (<see cref="GameContext.TargetTheory"/>): привести <see cref="BaseCount"/> адептов
/// и ещё по одному за каждого профессионала в теории, включая выложенного. Закрытые карты — граждане, не профессионалы.
/// </summary>
public sealed class AddAdeptsPerProfessionalEffect : Effect
{
  /// <summary>
  /// Сколько адептов приходит в любом случае.
  /// </summary>
  public int BaseCount { get; }

  public AddAdeptsPerProfessionalEffect(int baseCount)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(baseCount);
    BaseCount = baseCount;
  }

  public override string Description => $"Привести в теорию {BaseCount} адептов и ещё по одному за каждого профессионала в ней";

  public override void Execute(GameContext context)
  {
    var theory = context.TargetTheory ?? throw new InvalidOperationException("Не указан штаб теории.");
    var count = BaseCount + theory.Cards.Count(card => card is ProfessionalCard);
    if (count > 0 && !theory.IsClosed)
      context.Field.AddAdepts(theory, count);
  }
}
