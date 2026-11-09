using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// При выкладывании в штаб теории (<see cref="GameContext.TargetTheory"/>): обучить бездомного в этой теории
/// (<see cref="GameContext.TargetCell"/>) до гражданина — его заменяет первый гражданин из колоды рынка с той же теорией
/// или без теории, а бездомный уходит из игры. За обучение игрок получает деньги: <see cref="OwnMoney"/>$, если бездомный
/// его, и <see cref="ForeignMoney"/>$, если чужой. Без обучения денег нет.
/// </summary>
public sealed class TrainHomelessEffect : Effect
{
  /// <summary>
  /// Сколько $ за обучение своего персонажа.
  /// </summary>
  public const int OwnMoney = 2;

  /// <summary>
  /// Сколько $ за обучение чужого персонажа.
  /// </summary>
  public const int ForeignMoney = 4;

  public override string Description =>
    $"Обучить бездомного в теории до гражданина; получить {OwnMoney}$ за своего и {ForeignMoney}$ за чужого";

  public override bool RequiresTargetCell => true;

  public override bool TargetIsOptional => true;

  protected internal override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCellsCore(GameContext context) =>
    context.Field.TrainHomelessTargets(Theory(context), Market(context));

  protected internal override PlacementCheck CheckTargetCellCore(Cell cell, GameContext context) =>
    context.Field.CheckTrainHomeless(cell, Theory(context), Market(context));

  public override void Execute(GameContext context)
  {
    var theory = Theory(context);
    if (context.TargetCell is not { } cell)
      return;

    // Владелец обучаемого персонажа определяет награду: до обучения карточка ещё в стопке.
    var trainedOwner = theory.OwnerAt(cell)
      ?? throw new InvalidOperationException("В ячейке нет обучаемого персонажа.");
    context.Field.TrainHomeless(cell, theory, Market(context));

    var money = trainedOwner == context.CurrentPlayer ? OwnMoney : ForeignMoney;
    context.Field.EarnAt(context.SourceCell, context.CurrentPlayer, money);
  }

  private static TheoryColumn Theory(GameContext context) =>
    context.TargetTheory ?? throw new InvalidOperationException("Не указан штаб теории.");

  private static Market Market(GameContext context) =>
    context.Market ?? throw new InvalidOperationException("Для обучения нужен рынок.");
}
