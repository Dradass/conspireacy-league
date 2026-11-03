using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Выбрать персонажа в личном штабе (<see cref="GameContext.TargetCell"/>) и снизить его успех на <see cref="Reduction"/>%.
/// Игрок получает за это его влияние (убеждение) в $.
/// </summary>
public sealed class WeakenCharacterEffect : Effect
{
  /// <summary>
  /// На сколько процентов снижается успех.
  /// </summary>
  public const int Reduction = 10;

  public override string Description => $"Снизить успех любого персонажа в личном штабе на {Reduction}% и получить $, равные его влиянию";

  public override bool RequiresTargetCell => true;

  public override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCells(GameContext context) =>
    context.Field.WeakenTargets(context.CurrentPlayer);

  public override PlacementCheck CheckTargetCell(Cell cell, GameContext context) =>
    context.Field.CheckWeaken(cell, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбран персонаж.");
    var person = (PersonCard)context.Field.CardAt(cell)!;
    context.Field.Weaken(cell, context.CurrentPlayer, Reduction);
    context.CurrentPlayer.Earn(person.Influence);
  }
}
