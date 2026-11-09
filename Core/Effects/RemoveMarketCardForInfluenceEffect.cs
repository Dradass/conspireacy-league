using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Убрать карточку с рынка (<see cref="GameContext.TargetCell"/>) из игры; игрок получает её влияние (убеждение) в $.
/// </summary>
public sealed class RemoveMarketCardForInfluenceEffect : Effect
{
  public override string Description => "Убрать персонажа с рынка и получить $, равные его влиянию";

  public override bool RequiresTargetCell => true;

  protected internal override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCellsCore(GameContext context) =>
    (context.Market ?? throw new InvalidOperationException("Нет рынка."))
      .OpenCards.Keys
      .Select(cell => (cell, PlacementCheck.Allowed));

  protected internal override PlacementCheck CheckTargetCellCore(Cell cell, GameContext context) =>
    context.Market?.OpenCards.ContainsKey(cell) == true
      ? PlacementCheck.Allowed
      : PlacementCheck.Deny("На рынке здесь нет карточки.");

  public override void Execute(GameContext context)
  {
    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбрана карточка на рынке.");
    var card = (context.Market ?? throw new InvalidOperationException("Нет рынка.")).RemoveOpenCard(cell);
    context.CurrentPlayer.Earn(card.Influence);
  }
}
