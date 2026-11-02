using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// При выкладывании в штаб теории (<see cref="GameContext.TargetTheory"/>): обучить бездомного в этой теории
/// (<see cref="GameContext.TargetCell"/>) до гражданина — его заменяет первый гражданин из колоды рынка с той же теорией
/// или без теории, а бездомный уходит из игры. Затем получить <see cref="MoneyPerCitizen"/>$ за каждого гражданина
/// в теории (закрытые карты тоже граждане).
/// </summary>
public sealed class TrainHomelessEffect : Effect
{
  /// <summary>
  /// Сколько $ за каждого гражданина в теории.
  /// </summary>
  public const int MoneyPerCitizen = 2;

  public override string Description =>
    $"Обучить бездомного в теории до гражданина; получить {MoneyPerCitizen}$ за каждого гражданина в теории";

  public override bool RequiresTargetCell => true;

  public override bool TargetIsOptional => true;

  public override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCells(GameContext context) =>
    context.Field.TrainHomelessTargets(Theory(context), Market(context));

  public override PlacementCheck CheckTargetCell(Cell cell, GameContext context) =>
    context.Field.CheckTrainHomeless(cell, Theory(context), Market(context));

  public override void Execute(GameContext context)
  {
    var theory = Theory(context);
    if (context.TargetCell is { } cell)
      context.Field.TrainHomeless(cell, theory, Market(context));

    var citizens = theory.Cards.Count(card => card is CitizenCard or HiddenCard);
    context.Field.EarnAt(context.SourceCell, context.CurrentPlayer, citizens * MoneyPerCitizen);
  }

  private static TheoryColumn Theory(GameContext context) =>
    context.TargetTheory ?? throw new InvalidOperationException("Не указан штаб теории.");

  private static Market Market(GameContext context) =>
    context.Market ?? throw new InvalidOperationException("Для обучения нужен рынок.");
}
