using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Быстрое действие: поставить значок руля (<see cref="PlayingField.Wheels"/>) на своего персонажа в личном штабе
/// (<see cref="GameContext.TargetCell"/>). После вашего паса персонажи со значком руля возвращаются к вам на руку.
/// Игрок получает <see cref="Reward"/>$ сразу.
/// </summary>
public sealed class MarkForReturnEffect : Effect
{
  /// <param name="reward">Сколько $ получить за значок руля.</param>
  public MarkForReturnEffect(int reward = 0)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(reward);
    Reward = reward;
  }

  /// <summary>
  /// Сколько $ получить за значок руля.
  /// </summary>
  public int Reward { get; }

  public override string Description =>
    Reward > 0
      ? $"Быстрое: поставить руль на своего персонажа — после вашего паса он вернётся к вам на руку, и вы получите {Reward}$"
      : "Быстрое: поставить руль на своего персонажа — после вашего паса он вернётся к вам на руку";

  public override bool IsQuick => true;

  public override bool RequiresTargetCell => true;

  protected internal override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCellsCore(GameContext context) =>
    context.Field.WheelTargets(context.CurrentPlayer, Acting(context));

  protected internal override PlacementCheck CheckTargetCellCore(Cell cell, GameContext context) =>
    context.Field.CheckWheel(cell, context.CurrentPlayer, Acting(context));

  public override void Execute(GameContext context)
  {
    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбран персонаж.");
    context.Field.MarkForReturn(cell, context.CurrentPlayer, Acting(context));
    context.Field.EarnAt(context.SourceCell, context.CurrentPlayer, Reward);
  }

  /// <summary>
  /// Карточки, которые выполняют действие: та, что его играет, и та, чьё действие она использует.
  /// </summary>
  private static Cell[] Acting(GameContext context) =>
    new[] { context.SourceCell, context.BorrowedCell }.OfType<Cell>().ToArray();
}
