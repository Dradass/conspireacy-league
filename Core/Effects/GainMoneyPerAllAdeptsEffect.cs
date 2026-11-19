using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Игрок получает 1$ за каждые <see cref="GainMoneyPerAdeptEffect.AdeptsPerDollar"/> адепта в выбранном штабе теории
/// (<see cref="GameContext.TargetTheory"/>) — любом, не только своём.
/// </summary>
public sealed class GainMoneyPerAllAdeptsEffect : Effect
{
  public override string Description =>
    $"Получить 1$ за каждые {GainMoneyPerAdeptEffect.AdeptsPerDollar} адепта в любом штабе теории";

  public override bool RequiresTheory => true;

  public override void Execute(GameContext context)
  {
    var theory = context.TargetTheory ?? throw new InvalidOperationException("Не выбрана теория.");
    context.CurrentPlayer.Earn(theory.Adepts / GainMoneyPerAdeptEffect.AdeptsPerDollar);
  }
}
