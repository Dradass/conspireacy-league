using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Passives;

/// <summary>
/// Пассивная способность карточки теории «Коммунисты на Луне»: при добавлении лидера в штаб теории выкладывается
/// <see cref="MeeplesPerLeader"/> миплов вместо одного.
/// </summary>
/// <remarks>
/// Количество миплов считает <see cref="Field.PlayingField.AddLeaderMeeple"/>.
/// </remarks>
public sealed class CommunistsOnMoonPassive : PassiveAbility
{
  /// <summary>
  /// Сколько миплов выкладывается при добавлении лидера.
  /// </summary>
  public const int MeeplesPerLeader = 2;

  public override string Description => $"При добавлении лидера в штаб теории выкладывается {MeeplesPerLeader} миплов вместо одного";

  public override void Handle(GameEvent gameEvent, GameContext context)
  {
  }
}
