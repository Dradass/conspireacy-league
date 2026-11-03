using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность карточки теории «Плоская Земля»: основатель этой теории имеет
/// <see cref="FounderBonus"/> влияния, а её штаб нельзя закрыть.
/// </summary>
/// <remarks>
/// Влияние основателя и запрет закрытия учитывает <see cref="Field.PlayingField"/>.
/// </remarks>
public sealed class FlatEarthPassive : PassiveAbility
{
  /// <summary>
  /// На сколько влияния больше у основателя теории.
  /// </summary>
  public const int FounderBonus = 3;

  public override string Description =>
    $"Основатель теории имеет +{FounderBonus} влияния. Штаб этой теории нельзя закрыть";

  public override void Handle(GameEvent gameEvent, GameContext context)
  {
  }
}
