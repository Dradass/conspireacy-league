using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Добавить <see cref="Adepts"/> адептов в любые теории: по одному в каждую выбранную (<see cref="GameContext.TargetTheories"/>);
/// одну теорию можно выбрать несколько раз — хоть всех адептов в одну.
/// </summary>
public sealed class AddAdeptsToThreeTheoriesEffect : Effect
{
  /// <summary>
  /// Сколько адептов расставляют.
  /// </summary>
  public const int Adepts = 3;

  public override string Description => $"Добавить {Adepts} адептов в любые теории (можно всех в одну)";

  public override bool RequiresTheory => true;

  public override int TheoryCount => Adepts;

  public override bool AllowsRepeatedTheories => true;

  public override void Execute(GameContext context)
  {
    foreach (var group in context.TargetTheories.GroupBy(theory => theory))
      context.Field.AddAdepts(group.Key, group.Count());
  }
}
