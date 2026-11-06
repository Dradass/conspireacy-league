using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Поставить мипла влияния своего цвета лидером в выбранную теорию (<see cref="Field.TheoryColumn.LeaderMeeples"/>).
/// </summary>
public sealed class PlaceLeaderMeepleEffect : Effect
{
  public override string Description => "Добавить мипла влияния в теорию";

  public override bool RequiresTheory => true;

  /// <summary>
  /// Мипла нельзя поставить в защищённую полицейским теорию.
  /// </summary>
  public override bool CanTargetTheory(TheoryColumn column, Player player) =>
    base.CanTargetTheory(column, player) && !column.IsProtected();

  public override void Execute(GameContext context)
  {
    var theory = context.TargetTheory ?? throw new InvalidOperationException("Не выбрана теория.");
    context.Field.AddLeaderMeeple(theory, context.CurrentPlayer);
  }
}
