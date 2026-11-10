using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Адепт из первой теории слева и из первой справа, где есть адепты, переходит в эту теорию (<see cref="GameContext.TargetTheory"/>).
/// </summary>
public sealed class MoveNeighbourAdeptsEffect : Effect
{
  public override string Description => "По 1 адепту из первой теории слева и первой справа, где есть адепты, переходит в эту теорию";

  public override void Execute(GameContext context)
  {
    var home = context.TargetTheory ?? throw new InvalidOperationException("Не указана теория.");
    context.Field.MoveAdeptsFromNeighbours(home);
  }
}
