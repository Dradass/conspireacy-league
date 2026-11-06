namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность карточки теории «Иллюминаты»: после закрытия теории, где владелец лидер, её адепты
/// по-прежнему приносят деньги, и владелец получает свою долю. Его закрытые карточки при закрытии теории считаются бездомными.
/// </summary>
/// <remarks>
/// Доход считает <see cref="Field.PlayingField.IlluminatiBeneficiary"/>; подсчёт бездомных — <see cref="TheoryColumn"/>.
/// </remarks>
public sealed class IlluminatiPassive : PassiveAbility
{
  public override string Description =>
    "Адепты закрытой теории, где вы лидер, по-прежнему приносят вам деньги. Ваши закрытые карты при закрытии теории считаются бездомными";

  public override void Handle(Game.GameEvent gameEvent, Game.GameContext context)
  {
  }
}
