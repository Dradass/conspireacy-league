using ConspiracyLeague.Core.Cards.Theories;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность карточки теории «Ковид создан правительством»: в конце раунда владелец карточки
/// получает деньги за каждый свой закрытый штаб теории (<see cref="CovidHoaxCard.MoneyPerClosedHeadquarters"/>$).
/// </summary>
public sealed class CovidHoaxPassive : PassiveAbility
{
  public override string Description =>
    $"В конце раунда: {CovidHoaxCard.MoneyPerClosedHeadquarters}$ за каждый ваш закрытый штаб теории";

  public override void Handle(GameEvent gameEvent, GameContext context)
  {
    if (gameEvent is not RoundEnded)
      return;

    var closed = context.Field.ClosedHeadquartersOf(context.CurrentPlayer).Count();
    context.CurrentPlayer.Earn(closed * CovidHoaxCard.MoneyPerClosedHeadquarters);
  }
}
