using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность карточки теории «Золотой миллиард»: все профессионалы владельца карточки
/// приносят на <see cref="ProfessionalBonus"/>$ больше.
/// </summary>
/// <remarks>
/// Бонус добавляет <see cref="Field.PlayingField"/> при розыгрыше профессионала из личного штаба.
/// </remarks>
public sealed class GoldenBillionPassive : PassiveAbility
{
  /// <summary>
  /// На сколько $ больше приносит каждый профессионал владельца.
  /// </summary>
  public const int ProfessionalBonus = 1;

  public override string Description => $"Профессионалы владельца приносят на {ProfessionalBonus}$ больше";

  public override void Handle(GameEvent gameEvent, GameContext context)
  {
  }
}
