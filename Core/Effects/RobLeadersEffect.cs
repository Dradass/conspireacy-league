using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// При выкладывании в штаб теории (<see cref="GameContext.TargetTheory"/>): забрать <see cref="Amount"/>$ за каждую
/// карту других игроков в теории. Если владельцу карты платить нечем, карта возвращается ему в руку,
/// а он получает из банка <see cref="Compensation"/>$.
/// </summary>
public sealed class RobLeadersEffect : Effect
{
  /// <summary>
  /// Сколько $ забирается за каждую карту.
  /// </summary>
  public const int Amount = 1;

  /// <summary>
  /// Сколько $ получает владелец, чья карта вернулась в руку.
  /// </summary>
  public const int Compensation = 2;

  public override string Description =>
    $"Забрать по {Amount}$ за каждую карту других игроков в теории; если платить нечем — карта возвращается "
    + $"владельцу в руку, а он получает {Compensation}$";

  public override void Execute(GameContext context)
  {
    var theory = context.TargetTheory ?? throw new InvalidOperationException("Не указан штаб теории.");
    context.Field.RobLeaders(theory, context.CurrentPlayer, context.SourceCell, Amount, Compensation);
  }
}
