using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность карточки теории «Сатанисты похищают детей»: бездомные приносят
/// <see cref="HomelessBonus"/>$ сверх обычного дохода.
/// </summary>
/// <remarks>
/// Бонус добавляет <see cref="Field.PlayingField"/> при розыгрыше бездомного.
/// </remarks>
public sealed class SatanistsPassive : PassiveAbility
{
  /// <summary>
  /// На сколько $ больше приносит каждый бездомный.
  /// </summary>
  public const int HomelessBonus = 1;

  public override string Description => $"Бездомные приносят на {HomelessBonus}$ больше";

  public override void Handle(GameEvent gameEvent, GameContext context)
  {
  }
}
