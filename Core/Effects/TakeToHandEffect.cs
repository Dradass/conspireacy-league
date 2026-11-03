using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Забрать своего персонажа из личного штаба (<see cref="GameContext.TargetCell"/>) в руку.
/// Карточку, которая играет это действие (и карточку, через которую его играют), забрать нельзя.
/// </summary>
public sealed class TakeToHandEffect : Effect
{
  /// <param name="reward">Сколько $ получить за то, что карточка ушла в руку.</param>
  public TakeToHandEffect(int reward = 0)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(reward);
    Reward = reward;
  }

  /// <summary>
  /// Сколько $ получить за то, что карточка ушла в руку.
  /// </summary>
  public int Reward { get; }

  public override string Description =>
    Reward > 0
      ? $"Забрать своего персонажа из личного штаба в руку и получить {Reward}$"
      : "Забрать своего персонажа из личного штаба в руку";

  public override bool RequiresTargetCell => true;

  public override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCells(GameContext context) =>
    context.Field.TakeTargets(context.CurrentPlayer, Acting(context));

  public override PlacementCheck CheckTargetCell(Cell cell, GameContext context) =>
    context.Field.CheckTake(cell, context.CurrentPlayer, Acting(context));

  public override void Execute(GameContext context)
  {
    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбран персонаж.");
    context.Field.TakeToHand(cell, context.CurrentPlayer, Acting(context));
    context.Field.EarnAt(context.SourceCell, context.CurrentPlayer, Reward);
  }

  /// <summary>
  /// Карточки, которые выполняют действие: та, что его играет, и та, чьё действие она использует.
  /// </summary>
  private static Cell[] Acting(GameContext context) =>
    new[] { context.SourceCell, context.BorrowedCell }.OfType<Cell>().ToArray();
}
