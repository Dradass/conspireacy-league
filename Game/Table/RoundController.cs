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

  private Board _board = null!;
  private GameSession _session = null!;
  private TableCamera _camera = null!;

  /// <summary>
  /// Сообщение игрокам о том, что происходит.
  /// </summary>
  public event Action<string>? Hint;

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
      PlayRoundEnd();
  }

  private async void PlayRoundEnd()
  {
    // Фаза сменилась посреди нажатия «Пас» — начинаем со следующего кадра.
    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    _camera.FocusOn(Board.ZoneCenter(_session.Field.CommonZone));
    var closed = _session.CloseInsolventTheories();
    Hint?.Invoke(closed.Count == 0
      ? "Все спасовали. Проверка штабов теорий: все штабы состоятельны."
      : $"Все спасовали. Закрыты несостоятельные штабы: {string.Join(", ", closed.Select(column => $"«{column.Theory}»"))}.");
    await Pause(StepPause);

    while (_session.PayNextTheoryIncome() is { } income)
    {
      _camera.FocusOn(Board.CellCenter(income.Column.TheoryCell.ToVector2I()));
      Hint?.Invoke($"Адепты теории «{income.Column.Theory}» приносят {income.Total}$.");
      await _board.ShowIncome(income);
    }

    var zones = _session.Field.PlayerZones.Values.ToList();
    _camera.FocusOn((Board.ZoneCenter(zones[0]) + Board.ZoneCenter(zones[^1])) / 2);
    Hint?.Invoke("Обновление карточек: сыгранные карточки личных штабов переворачиваются.");
    await Pause(CameraPause);
    _session.RefreshPlayedCards();
    await Pause(StepPause);

    _camera.FocusOn(Board.ZoneCenter(_session.Market.Zone));
    Hint?.Invoke("Обновление рынка.");
    await Pause(CameraPause);
    if (!_session.RefreshMarket())
    {
      Hint?.Invoke("В колоде рынка не осталось карточек — партия заканчивается. Итоговый подсчёт денег.");
      await Pause(StepPause);
      await PlayFinalScoring();
      return;
    }

    await Pause(MarketPause);

    _session.StartNextRound();
    Hint?.Invoke($"Раунд {_session.Round}. Ходит {_session.CurrentPlayer}.");
  }

  /// <summary>
  /// Итоговый подсчёт: владельцы действующих теорий получают деньги за лидеров, затем адепты ещё раз
  /// приносят деньги. После этого партия завершена, и интерфейс показывает итоги.
  /// </summary>
  private async Task PlayFinalScoring()
  {
    while (_session.PayNextLeadersBonus() is { } bonus)
    {
      _camera.FocusOn(Board.CellCenter(bonus.Column.TheoryCell.ToVector2I()));
      Hint?.Invoke($"«{bonus.Column.Theory}»: владелец {bonus.Owner} получает {bonus.Amount}$ за лидеров.");
      await _board.ShowLeadersBonus(bonus);
    }

    while (_session.PayNextTheoryIncome() is { } income)
    {
      _camera.FocusOn(Board.CellCenter(income.Column.TheoryCell.ToVector2I()));
      Hint?.Invoke($"Повторный доход: адепты теории «{income.Column.Theory}» приносят {income.Total}$.");
      await _board.ShowIncome(income);
    }
  }

  private SignalAwaiter Pause(double seconds) => ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
}
