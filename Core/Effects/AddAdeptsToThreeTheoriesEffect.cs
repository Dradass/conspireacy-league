using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Добавить по <see cref="Adepts"/> адептов в каждую из трёх выбранных теорий (<see cref="GameContext.TargetTheories"/>).
/// </summary>
public sealed class AddAdeptsToThreeTheoriesEffect : Effect
{
  /// <summary>
  /// Сколько теорий выбирают.
  /// </summary>
  public const int Theories = 3;

  /// <summary>
  /// Сколько адептов приходит в каждую теорию.
  /// </summary>
  public const int Adepts = 3;

  public override string Description => $"Добавить {Adepts} адептов в любые {Theories} теории";

  public override bool RequiresTheory => true;

  public override int TheoryCount => Theories;

  public override void Execute(GameContext context)
  {
    foreach (var theory in context.TargetTheories)
      context.Field.AddAdepts(theory, Adepts);
  }
}
