using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// При выкладывании в штаб теории (<see cref="GameContext.TargetTheory"/>): можно закрыть любого лидера в этой теории
/// (<see cref="GameContext.TargetCell"/>) — он становится закрытой картой своего владельца. Затем получить
/// <see cref="MoneyPerHidden"/>$ за каждую закрытую (нераскрытую) карту в теории.
/// </summary>
public sealed class CloseLeaderEffect : Effect
{
  /// <summary>
  /// Сколько $ за каждую закрытую карту в теории.
  /// </summary>
  public const int MoneyPerHidden = 2;

  public override string Description =>
    $"Можно закрыть любого лидера в теории; получить {MoneyPerHidden}$ за каждую закрытую карту в теории";

  public override bool RequiresTargetCell => true;

  public override bool TargetIsOptional => true;

  protected internal override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCellsCore(GameContext context) =>
    context.Field.CloseLeaderTargets(Theory(context), context.SourceCell);

  protected internal override PlacementCheck CheckTargetCellCore(Cell cell, GameContext context) =>
    context.Field.CheckCloseLeader(cell, Theory(context), context.SourceCell);

  public override void Execute(GameContext context)
  {
    var theory = Theory(context);
    if (context.TargetCell is { } cell)
      context.Field.CloseLeader(cell, theory, context.SourceCell);

    var hidden = theory.Cards.Count(card => card is HiddenCard { IsRevealed: false });
    context.Field.EarnAt(context.SourceCell, context.CurrentPlayer, hidden * MoneyPerHidden);
  }

  private static TheoryColumn Theory(GameContext context) =>
    context.TargetTheory ?? throw new InvalidOperationException("Не указан штаб теории.");
}
