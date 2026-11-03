using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность карточки теории «Эбола создана ЦРУ»: когда другой игрок добавляет нового лидера в штаб теории
/// с персонажем владельца, владелец получает <see cref="HolderBonus"/>$, а добавивший платит ему <see cref="LeaderPayment"/>$.
/// </summary>
/// <remarks>
/// Выплаты делает <see cref="Field.PlayingField"/>.
/// </remarks>
public sealed class EbolaPassive : PassiveAbility
{
  /// <summary>
  /// Сколько $ владелец получает из банка.
  /// </summary>
  public const int HolderBonus = 3;

  /// <summary>
  /// Сколько $ платит владелец нового лидера.
  /// </summary>
  public const int LeaderPayment = 1;

  public override string Description =>
    $"Когда другой игрок добавляет лидера в теорию с вашим персонажем: вы получаете {HolderBonus}$, а он платит вам {LeaderPayment}$";

  public override void Handle(GameEvent gameEvent, GameContext context)
  {
  }
}
