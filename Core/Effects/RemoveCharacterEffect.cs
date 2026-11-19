using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Убрать из игры персонажа из своего личного штаба (<see cref="GameContext.TargetCell"/>).
/// Игрок получает его стоимость и влияние.
/// </summary>
public sealed class RemoveCharacterEffect : Effect
{
  public override string Description => "Убрать персонажа из своего личного штаба и получить его стоимость и влияние в $";

  public override bool RequiresTargetCell => true;

  protected internal override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCellsCore(GameContext context) =>
    context.Field.RemovableCharacterTargets(context.CurrentPlayer);

  protected internal override PlacementCheck CheckTargetCellCore(Cell cell, GameContext context) =>
    context.Field.CheckRemoveCharacter(cell, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбран персонаж.");
    context.Field.RemoveCharacter(cell, context.CurrentPlayer);
  }
}
