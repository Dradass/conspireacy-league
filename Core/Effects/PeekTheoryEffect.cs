using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// При выкладывании в штаб теории (<see cref="GameContext.TargetTheory"/>): бесплатно посмотреть все закрытые карты
/// других игроков в этой теории.
/// </summary>
public sealed class PeekTheoryEffect : Effect
{
  public override string Description => "Посмотреть все закрытые карты в этой теории";

  public override void Execute(GameContext context)
  {
    var theory = context.TargetTheory ?? throw new InvalidOperationException("Не указан штаб теории.");
    context.Field.PeekTheory(theory, context.CurrentPlayer, context.SourceCell);
  }
}
