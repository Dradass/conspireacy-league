using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Поставить мипла влияния своего цвета лидером в выбранную теорию (<see cref="Field.TheoryColumn.LeaderMeeples"/>).
/// </summary>
public sealed class PlaceLeaderMeepleEffect : Effect
{
  public override string Description => "Добавить мипла влияния в теорию";

  public override bool RequiresTheory => true;

  public override void Execute(GameContext context)
  {
    var theory = context.TargetTheory ?? throw new InvalidOperationException("Не выбрана теория.");
    context.Field.AddLeaderMeeple(theory, context.CurrentPlayer);
  }
}
