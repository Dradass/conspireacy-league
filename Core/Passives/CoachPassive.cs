using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность тренера в личном штабе: один соседний персонаж с самой низкой вероятностью успеха
/// получает +<see cref="SuccessBonus"/>% всегда (из равных — случайный). Бонус считает
/// <see cref="Field.PlayingField"/>: <see cref="Field.PlayingField.CoachBonusAt"/>.
/// </summary>
public sealed class CoachPassive : PassiveAbility
{
  /// <summary>
  /// На сколько процентов тренер повышает вероятность успеха выбранного соседа.
  /// </summary>
  public const int SuccessBonus = 50;

  public override string Description =>
    $"Постоянно: сосед в личном штабе с самым низким шансом успеха получает +{SuccessBonus}% (из равных — случайный)";

  public override void Handle(GameEvent gameEvent, GameContext context)
  {
  }
}
