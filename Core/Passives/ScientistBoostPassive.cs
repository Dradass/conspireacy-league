namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность карточки теории «Технологии от инопланетян»: все ученые владельца дают больше.
/// Соседи каждого ученого получают <see cref="SuccessBonus"/>% к успеху вместо обычных +10%, а ученый приносит +<see cref="MoneyBonus"/>$
/// к деньгам соседних персонажей — сверх обычных бонусов учёного (<see cref="ScientistPassive"/>).
/// </summary>
/// <remarks>
/// Бонусы считает <see cref="Field.PlayingField"/>.
/// </remarks>
public sealed class ScientistBoostPassive : PassiveAbility
{
  /// <summary>
  /// Сколько % к успеху получают соседи каждого ученого владельца (вместо обычных 10%).
  /// </summary>
  public const int SuccessBonus = 15;

  /// <summary>
  /// Сколько $ добавляет каждый ученый владельца к деньгам соседних персонажей.
  /// </summary>
  public const int MoneyBonus = 2;

  public override string Description =>
    $"Ваши ученые дают соседям {SuccessBonus}% успеха (вместо +10%) и +{MoneyBonus}$ к деньгам, которые те приносят";

  public override void Handle(Game.GameEvent gameEvent, Game.GameContext context)
  {
  }
}
