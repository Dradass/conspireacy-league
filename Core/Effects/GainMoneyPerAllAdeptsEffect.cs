using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Игрок получает 1$ за каждые <see cref="GainMoneyPerAdeptEffect.AdeptsPerDollar"/> адепта во всех штабах теорий.
/// </summary>
public sealed class GainMoneyPerAllAdeptsEffect : Effect
{
  public override string Description =>
    $"Получить 1$ за каждые {GainMoneyPerAdeptEffect.AdeptsPerDollar} адепта в любом штабе теории";

  public override void Execute(GameContext context)
  {
    var adepts = context.Field.TheoryColumns.Sum(column => column.Adepts);
    context.CurrentPlayer.Earn(adepts / GainMoneyPerAdeptEffect.AdeptsPerDollar);
  }
}
