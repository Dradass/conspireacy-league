namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Фаза партии.
/// </summary>
/// <remarks>
/// Подготовка выполняется один раз при старте. Дальше идут раунды: ходы игроков, проверка штабов теорий,
/// доход от адептов и обновление рынка.
/// </remarks>
public enum GamePhase
{
  /// <summary>
  /// Подготовка: колода перемешана, игрокам раздаются стартовые карточки, на рынке открываются карточки.
  /// </summary>
  Setup,

  /// <summary>
  /// Ходы игроков — пока все не спасуют.
  /// </summary>
  PlayerTurns,

  /// <summary>
  /// Проверка штабов теорий: несостоятельные закрываются.
  /// </summary>
  HeadquartersCheck,

  /// <summary>
  /// Адепты несут деньги лидерам действующих теорий.
  /// </summary>
  AdeptIncome,

  /// <summary>
  /// Обновление рынка.
  /// </summary>
  MarketRefresh,
}
