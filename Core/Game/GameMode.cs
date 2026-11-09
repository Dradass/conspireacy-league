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

  /// <summary>
  /// Гонка: как только любая теория доходит до финиша (<see cref="RaceScoring.FinishLeaders"/>), раунд объявляется
  /// последним. Очки (<see cref="RaceScore"/>) получают только действующие теории, которые к концу стоят на финише.
  /// </summary>
  Race,
}
