namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность карточки теории «Ленин — гриб»: когда персонаж владельца в личном штабе
/// неудачно проходит проверку успеха, владелец получает <see cref="MoneyPerFailure"/>$.
/// </summary>
/// <remarks>
/// Деньги начисляет <see cref="Field.PlayingField"/> при неудачной проверке.
/// </remarks>
public sealed class LeninMushroomPassive : PassiveAbility
{
  /// <summary>
  /// Сколько $ получает владелец за каждый неудачный розыгрыш своего персонажа.
  /// </summary>
  public const int MoneyPerFailure = 1;

  public override string Description => $"Неудачная проверка успеха вашего персонажа: +{MoneyPerFailure}$";

  public override void Handle(Game.GameEvent gameEvent, Game.GameContext context)
  {
  }
}
