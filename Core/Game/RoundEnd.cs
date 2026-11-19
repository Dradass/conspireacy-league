using ConspiracyLeague.Core.Field;

namespace ConspiracyLeague.Core.Game;

/// <summary>
/// Конец раунда без анимаций — на сервере и в партиях ботов: проверка штабов, доход от адептов, обновление карточек
/// и рынка, а если раунды закончились — итоговый подсчёт. Тот же порядок шагов, что в интерфейсе (RoundController).
/// </summary>
public static class RoundEnd
{
  /// <summary>
  /// Провести конец раунда целиком. Вызывается, когда все спасовали (<see cref="GamePhase.HeadquartersCheck"/>).
  /// После этого идёт следующий раунд (<see cref="GamePhase.PlayerTurns"/>) или партия завершена (<see cref="GamePhase.GameOver"/>).
  /// </summary>
  /// <param name="session">Партия.</param>
  /// <param name="onIncome">Вызывается на каждую выплату дохода от адептов теории.</param>
  /// <param name="onBonus">Вызывается на каждую выплату за лидеров в итоговом подсчёте.</param>
  /// <exception cref="InvalidOperationException">Раунд ещё не закончился.</exception>
  public static void Run(GameSession session, Action<TheoryIncome>? onIncome = null, Action<LeadersBonus>? onBonus = null)
  {
    if (session.Phase != GamePhase.HeadquartersCheck)
      throw new InvalidOperationException($"Конец раунда проводится в фазе {GamePhase.HeadquartersCheck}, а сейчас {session.Phase}.");

    session.CloseInsolventTheories();
    while (session.PayNextTheoryIncome() is { } income)
      onIncome?.Invoke(income);

    session.RefreshPlayedCards();
    if (session.RefreshMarket())
    {
      session.StartNextRound();
      return;
    }

    // Владельцы закрытых персонажей решают, кого раскрыть; без интерфейса за всех решает бот.
    while (session.Phase == GamePhase.FinalReveal)
      Bots.Bot.PlayFinalReveal(session);

    if (session.Phase != GamePhase.FinalLeadersBonus)
      return;

    while (session.PayNextLeadersBonus() is { } bonus)
      onBonus?.Invoke(bonus);

    while (session.PayNextTheoryIncome() is { } income)
      onIncome?.Invoke(income);
  }
}
