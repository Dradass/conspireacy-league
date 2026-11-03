namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность карточки теории «Иллюминаты»: когда в теорию с закрытым персонажем владельца
/// добавляется чужой лидер, владелец получает <see cref="Payment"/>$ из банка.
/// </summary>
/// <remarks>
/// Выплату делает <see cref="Field.PlayingField"/>.
/// </remarks>
public sealed class IlluminatiPassive : PassiveAbility
{
  /// <summary>
  /// Сколько $ владелец получает из банка.
  /// </summary>
  public const int Payment = 5;

  public override string Description =>
    $"Когда в теорию с вашим закрытым персонажем приходит чужой лидер, вы получаете {Payment}$ из банка";

  public override void Handle(Game.GameEvent gameEvent, Game.GameContext context)
  {
  }
}
