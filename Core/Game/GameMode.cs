namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Режим партии: как определяется победитель.
/// </summary>
public enum GameMode
{
  /// <summary>
  /// Заработок денег: побеждает тот, у кого больше всего денег.
  /// </summary>
  Money,

  /// <summary>
  /// Развитие теорий: побеждает тот, у кого больше всего победных очков (<see cref="VictoryScore"/>).
  /// </summary>
  TheoryDevelopment,
}
