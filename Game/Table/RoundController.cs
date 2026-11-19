using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;
using Godot;

namespace ConspiracyLeague.Game.Table;

/// <summary>
/// Конец раунда: когда все игроки спасовали, по очереди проводит шаги конца раунда и показывает их на столе.
/// </summary>
/// <remarks>
/// Проверка штабов теорий (несостоятельные закрываются), доход от адептов — по одной теории, с анимацией,
/// обновление карточек личных штабов, обновление рынка, затем начинается следующий раунд. Если обновлять рынок
/// нечем — колода кончилась, — вместо следующего раунда идёт итоговый подсчёт денег, и партия завершается.
/// Камера переезжает туда, где что-то происходит.
/// Сами правила — в <see cref="GameSession"/>.
/// </remarks>
public partial class RoundController : Node
{
  private const double StepPause = 1.0;
  private const double CameraPause = 0.5;
  private const double MarketPause = 1.0;
  private const double PollInterval = 0.1;

  private Board _board = null!;
  private GameSession _session = null!;
  private TableCamera _camera = null!;

  /// <summary>
  /// Показывать анимацию получения денег: камера по очереди едет к каждой теории, над карточками всплывают суммы.
  /// Выключено — деньги начисляются сразу, одним шагом.
  /// </summary>
  public bool IncomeAnimation { get; set; }

  /// <summary>
  /// Сообщение игрокам о том, что происходит.
  /// </summary>
  public event Action<string>? Hint;

  /// <summary>
  /// Начался новый раунд: нужно показать сводку конца прошлого раунда (доход от адептов, закрытые и открытые теории).
  /// </summary>
  public event Action<IReadOnlyList<TheoryIncome>, SolvencyChanges>? IncomeSummaryRequested;

  /// <summary>
  /// Настроить контроллер.
  /// </summary>
  /// <param name="board">Стол.</param>
  /// <param name="session">Сессия игры.</param>
  /// <param name="camera">Камера: показывает каждый шаг конца раунда.</param>
  public void Initialize(Board board, GameSession session, TableCamera camera)
  {
    _board = board;
    _session = session;
    _camera = camera;
    _session.PhaseChanged += OnPhaseChanged;
  }

  public override void _ExitTree() => _session.PhaseChanged -= OnPhaseChanged;

  private void OnPhaseChanged(GamePhase phase)
  {
    if (phase == GamePhase.HeadquartersCheck)
      PlayRoundEndSafely();
  }

  /// <summary>
  /// Провести конец раунда, не роняя игру: ошибку пишем в лог и показываем игрокам, а не теряем молча
  /// (иначе партия застряла бы в фазе конца раунда без единого сообщения).
  /// </summary>
  private async void PlayRoundEndSafely()
  {
    try
    {
      await PlayRoundEnd();
    }
    catch (Exception error)
    {
      // Партию закрыли посреди анимаций (выход в меню): узлов уже нет, сообщать некому.
      if (!IsInstanceValid(this) || !IsInsideTree())
        return;
      GD.PrintErr($"Ошибка в конце раунда (фаза {_session.Phase}): {error}");
      Hint?.Invoke($"Ошибка в конце раунда: {error.Message}");
    }
  }

  private async Task PlayRoundEnd()
  {
    // Фаза сменилась посреди нажатия «Пас» — начинаем со следующего кадра.
    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    _camera.FocusOn(_board.ZoneCenter(_session.Field.CommonZone));
    var changes = _session.CloseInsolventTheories();
    var closed = changes.Closed;
    var reopened = changes.Reopened;
    Hint?.Invoke(closed.Count == 0 && reopened.Count == 0
      ? "Все спасовали. Проверка штабов теорий: все штабы состоятельны."
      : $"Все спасовали. {string.Join(" ", new[]
      {
        closed.Count == 0 ? null : $"Закрыты несостоятельные штабы: {string.Join(", ", closed.Select(column => $"«{column.Theory}»"))}.",
        reopened.Count == 0 ? null : $"Открыты обратно: {string.Join(", ", reopened.Select(column => $"«{column.Theory}»"))}.",
      }.OfType<string>())}");
    await Pause(StepPause);

    if (IncomeAnimation)
    {
      while (_session.PayNextTheoryIncome() is { } income)
      {
        _camera.FocusOn(_board.CellCenter(income.Column.TheoryCell.ToVector2I()));
        Hint?.Invoke($"Адепты теории «{income.Column.Theory}» приносят {income.Total}$.");
        await _board.ShowIncome(income);
      }
    }
    else
    {
      var total = 0;
      while (_session.PayNextTheoryIncome() is { } income)
        total += income.Total;
      if (total > 0)
        Hint?.Invoke($"Адепты приносят деньги: всего {total}$.");
    }

    var zones = _session.Field.PlayerZones.Values.ToList();
    _camera.FocusOn((_board.ZoneCenter(zones[0]) + _board.ZoneCenter(zones[^1])) / 2);
    Hint?.Invoke("Обновление карточек: сыгранные карточки личных штабов переворачиваются.");
    await Pause(CameraPause);
    _session.RefreshPlayedCards();
    await Pause(StepPause);

    _camera.FocusOn(_board.ZoneCenter(_session.Market.Zone));
    Hint?.Invoke("Обновление рынка.");
    await Pause(CameraPause);
    if (!_session.RefreshMarket())
    {
      // Владельцы закрытых персонажей по очереди раскрывают, кого хотят (FinalRevealController, BotController).
      var revealed = "Закрытые карточки раскрыты.";
      if (_session.Phase == GamePhase.FinalReveal)
      {
        await WaitFinalReveal();
        revealed = "Владельцы раскрыли выбранных закрытых персонажей.";
      }
      if (_session.Mode == GameMode.Race)
      {
        Hint?.Invoke(_session.IsLastRound
          ? $"Последний раунд окончен — гонка завершена. {revealed} Подсчёт очков за теории на финише."
          : $"В колоде рынка не осталось карточек — гонка завершена. {revealed} Подсчёт очков за теории на финише.");
        return;
      }
      if (_session.Mode == GameMode.TheoryDevelopment)
      {
        // Очки считаются по итоговому состоянию поля: выплат в конце нет, партия сразу завершается.
        Hint?.Invoke($"В колоде рынка не осталось карточек — партия заканчивается. {revealed} Подсчёт очков.");
        return;
      }
      Hint?.Invoke($"В колоде рынка не осталось карточек — партия заканчивается. {revealed} Итоговый подсчёт денег.");
      await Pause(StepPause);
      await PlayFinalScoring();
      return;
    }

    await Pause(MarketPause);

    _session.StartNextRound();
    Hint?.Invoke($"Раунд {_session.Round}. Ходит {_session.CurrentPlayer}.");
    var solvency = _session.SolvencyOfRound;
    if (_session.IncomeOfRound.Count > 0 || solvency.Closed.Count > 0 || solvency.Reopened.Count > 0)
      IncomeSummaryRequested?.Invoke(_session.IncomeOfRound, solvency);
  }

  /// <summary>
  /// Итоговый подсчёт: владельцы действующих теорий получают деньги за лидеров, затем адепты ещё раз
  /// приносят деньги. После этого партия завершена, и интерфейс показывает итоги.
  /// </summary>
  private async Task PlayFinalScoring()
  {
    if (!IncomeAnimation)
    {
      while (_session.PayNextLeadersBonus() is not null)
      {
      }
      while (_session.PayNextTheoryIncome() is not null)
      {
      }
      return;
    }

    while (_session.PayNextLeadersBonus() is { } bonus)
    {
      _camera.FocusOn(_board.CellCenter(bonus.Column.TheoryCell.ToVector2I()));
      Hint?.Invoke($"«{bonus.Column.Theory}»: владелец {bonus.Owner} получает {bonus.Amount}$ за лидеров.");
      await _board.ShowLeadersBonus(bonus);
    }

    while (_session.PayNextTheoryIncome() is { } income)
    {
      _camera.FocusOn(_board.CellCenter(income.Column.TheoryCell.ToVector2I()));
      Hint?.Invoke($"Повторный доход: адепты теории «{income.Column.Theory}» приносят {income.Total}$.");
      await _board.ShowIncome(income);
    }
  }

  /// <summary>
  /// Дождаться, пока все владельцы закрытых персонажей закончат их раскрывать (<see cref="GamePhase.FinalReveal"/>).
  /// </summary>
  private async Task WaitFinalReveal()
  {
    // Подсказку, кто сейчас выбирает, показывает FinalRevealController.
    while (_session.Phase == GamePhase.FinalReveal)
      await Pause(PollInterval);
  }

  /// <summary>
  /// Пауза между шагами. Если за это время партию закрыли (выход в меню), продолжать нельзя: шаги дальше
  /// обращаются к уже удалённым узлам — останавливаем последовательность.
  /// </summary>
  private async Task Pause(double seconds)
  {
    await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    if (!IsInstanceValid(this) || !IsInsideTree())
      throw new OperationCanceledException("Партия закрыта.");
  }
}
