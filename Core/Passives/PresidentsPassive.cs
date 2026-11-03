using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность карточки теории «Двойники президентов»: политики приносят
/// <see cref="PoliticianBonus"/>$ сверх обычного дохода.
/// </summary>
/// <remarks>
/// Бонус добавляет <see cref="Field.PlayingField"/> при розыгрыше политика.
/// </remarks>
public sealed class PresidentsPassive : PassiveAbility
{
  /// <summary>
  /// На сколько $ больше приносит каждый политик.
  /// </summary>
  public const int PoliticianBonus = 2;

  public override string Description => $"Политики приносят на {PoliticianBonus}$ больше";

  public override void Handle(GameEvent gameEvent, GameContext context)
  {
  }
}
