using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Быстрое действие: навсегда повысить вероятность успеха персонажа в личном штабе (<see cref="GameContext.TargetCell"/>)
/// на <see cref="Bonus"/>%. Если персонаж чужой, игрок получает <see cref="RivalReward"/>$, если свой — <see cref="OwnReward"/>$.
/// Чужих персонажей под защитой полицейского выбрать нельзя.
/// </summary>
public sealed class TrainEffect : Effect
{
  /// <summary>
  /// На сколько процентов повышается вероятность успеха.
  /// </summary>
  public const int Bonus = 10;

  /// <summary>
  /// Сколько $ получает игрок, если тренирует чужого персонажа.
  /// </summary>
  public const int RivalReward = 4;

  /// <summary>
  /// Сколько $ получает игрок, если тренирует своего персонажа.
  /// </summary>
  public const int OwnReward = 2;

  public override string Description =>
    $"Быстрое: навсегда +{Bonus}% успеха любому персонажу (чужому — +{RivalReward}$, своему — +{OwnReward}$)";

  public override bool IsQuick => true;

  public override bool RequiresTargetCell => true;

  protected internal override IEnumerable<(Cell Cell, PlacementCheck Check)> TargetCellsCore(GameContext context) =>
    context.Field.TrainTargets(context.CurrentPlayer);

  protected internal override PlacementCheck CheckTargetCellCore(Cell cell, GameContext context) =>
    context.Field.CheckTrain(cell, context.CurrentPlayer);

  public override void Execute(GameContext context)
  {
    var cell = context.TargetCell ?? throw new InvalidOperationException("Не выбран персонаж.");
    var rival = context.Field.Train(cell, context.CurrentPlayer, Bonus);
    // Деньги уходят через текущий игрок-заработок: над карточкой тренера всплывает сумма (см. PlayingField.Use).
    context.CurrentPlayer.Earn(rival ? RivalReward : OwnReward);
  }
}
