using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Несколько действий подряд: «+1$ и приводит 1 адепта».
/// </summary>
public sealed class AllOfEffect : Effect
{
  /// <summary>
  /// Действия в порядке выполнения.
  /// </summary>
  public IReadOnlyList<Effect> Effects { get; }

  public AllOfEffect(params Effect[] effects)
  {
    if (effects.Length < 2)
      throw new ArgumentException("Нужно хотя бы два действия.", nameof(effects));
    Effects = effects;
  }

  public override string Description => string.Join(" и ", Effects.Select(e => e.Description));

  public override void Execute(GameContext context)
  {
    foreach (var effect in Effects)
      effect.Execute(context);
  }
}
