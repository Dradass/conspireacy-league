using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Положить карточку «Шапочки из фольги» на персонажа в личном штабе (<see cref="GameContext.TargetCell"/>).
/// Само прикрепление делает <see cref="PlayingField.UseTheoryCard"/>: здесь выбирают только персонажа.
/// </summary>
public sealed class AttachTinFoilEffect : Effect
{
  public override string Description =>
    "Положить на персонажа в личном штабе до конца раунда: он имеет 100% успеха и защищён от любых эффектов";

  public override bool RequiresTargetCell => true;

  protected internal override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCellsCore(GameContext context) =>
    context.Field.FoilTargets();

  protected internal override PlacementCheck CheckTargetCellCore(Cell cell, GameContext context) =>
    context.Field.CheckFoil(cell);

  public override void Execute(GameContext context)
  {
    if (context.TargetCell is null)
      throw new InvalidOperationException("Не выбран персонаж.");
  }
}
