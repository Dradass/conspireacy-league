using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Инспекция: выбрать другого игрока (<see cref="GameContext.TargetPlayer"/>), получить
/// <see cref="MoneyPerHomeless"/>$ за каждого бездомного в его личном штабе, а затем перевернуть рубашкой вверх
/// его незащищённого несыгранного бездомного с самой низкой вероятностью успеха (из равных — случайного).
/// </summary>
public sealed class InspectionEffect : Effect
{
  /// <summary>
  /// Сколько $ приносит каждый бездомный в личном штабе выбранного игрока.
  /// </summary>
  public const int MoneyPerHomeless = 1;

  public override string Description => "Инспекция: получить 1$ за каждого бездомного другого игрока и перевернуть одного";

  public override bool RequiresTargetPlayer => true;

  public override void Execute(GameContext context)
  {
    var target = context.TargetPlayer ?? throw new InvalidOperationException("Не выбран игрок для инспекции.");
    context.Field.Inspect(target, context.CurrentPlayer, MoneyPerHomeless, context.Random);
  }
}
