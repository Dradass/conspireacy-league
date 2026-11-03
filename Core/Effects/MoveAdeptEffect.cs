using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Перевести одного адепта из одной действующей теории (<see cref="GameContext.SourceTheory"/>)
/// в другую (<see cref="GameContext.TargetTheory"/>) и получить <see cref="Reward"/>$.
/// </summary>
public sealed class MoveAdeptEffect : Effect
{
  /// <summary>
  /// Сколько $ получает игрок.
  /// </summary>
  public const int Reward = 2;

  public override string Description => $"Перевести адепта из одной теории в другую и получить {Reward}$";

  public override bool RequiresTheory => true;

  public override bool RequiresSourceTheory => true;

  public override bool CanTargetSourceTheory(TheoryColumn column, Player player) =>
    !column.IsClosed && column.Adepts > 0 && !column.IsProtected() && !column.AdeptsShielded;

  public override void Execute(GameContext context)
  {
    var from = context.SourceTheory ?? throw new InvalidOperationException("Не выбрана теория, откуда забрать адепта.");
    var to = context.TargetTheory ?? throw new InvalidOperationException("Не выбрана теория, куда перевести адепта.");
    context.Field.MoveAdepts(from, to, 1);
    context.CurrentPlayer.Earn(Reward);
  }
}
