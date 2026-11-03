using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Theories;

namespace ConspiracyLeague.Core.Effects;

/// <summary>
/// Игрок получает по <see cref="PerLeader"/>$ за каждого лидера во всех штабах теорий коммунистов.
/// </summary>
public sealed class GainMoneyPerCommunistLeaderEffect : Effect
{
  /// <summary>
  /// Сколько $ за каждого лидера.
  /// </summary>
  public const int PerLeader = 1;

  public override string Description => $"Получить {PerLeader}$ за каждого лидера во всех штабах теорий коммунистов";

  public override void Execute(GameContext context) =>
    context.CurrentPlayer.Earn(
      context.Field.TheoryColumns.Where(column => column.Theory.BelongsTo(TheoryCatalog.CommunistConspiracy)).Sum(column => column.Leaders)
      * PerLeader);
}
