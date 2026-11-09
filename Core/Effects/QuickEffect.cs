using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Обычное действие, сыгранное как быстрое (<see cref="Effect.IsQuick"/>): «Быстрое: +2 адепта в теорию».
/// Всё остальное — цели и выполнение — как у исходного действия.
/// </summary>
public sealed class QuickEffect : Effect
{
  /// <summary>
  /// Исходное действие.
  /// </summary>
  public Effect Inner { get; }

  public QuickEffect(Effect inner)
  {
    ArgumentNullException.ThrowIfNull(inner);
    Inner = inner;
  }

  public override string Description =>
    $"Быстрое: {char.ToLower(Inner.Description[0])}{Inner.Description[1..]}";

  public override bool IsQuick => true;

  public override bool RequiresTargetPlayer => Inner.RequiresTargetPlayer;

  public override bool RequiresTheory => Inner.RequiresTheory;

  public override bool RequiresSourceTheory => Inner.RequiresSourceTheory;

  public override bool CanTargetSourceTheory(TheoryColumn column, Player player) => Inner.CanTargetSourceTheory(column, player);

  public override bool CanTargetTheory(TheoryColumn column, Player player) => Inner.CanTargetTheory(column, player);

  public override bool CanTargetAdeptCard(Cell cell, PlayingField field) => Inner.CanTargetAdeptCard(cell, field);

  public override bool RequiresHandCard => Inner.RequiresHandCard;

  public override bool RequiresTargetCell => Inner.RequiresTargetCell;

  public override bool BorrowsTargetCard => Inner.BorrowsTargetCard;

  protected internal override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCellsCore(GameContext context) => Inner.TargetCellsCore(context);

  protected internal override PlacementCheck CheckTargetCellCore(Cell cell, GameContext context) => Inner.CheckTargetCellCore(cell, context);

  public override void Execute(GameContext context) => Inner.Execute(context);
}
