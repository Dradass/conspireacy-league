using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Logging;
using ConspiracyLeague.Core.Views;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Запись истории партии.
/// </summary>
/// <param name="Round">Раунд, в котором это произошло (0 — подготовка).</param>
/// <param name="Player">Кто действовал; <c>null</c> — событие партии (начало раунда, конец партии).</param>
/// <param name="Text">Что произошло.</param>
/// <param name="IsRoundStart">Запись о начале раунда — в окне это разделитель.</param>
public sealed record HistoryEntry(int Round, Player? Player, string Text, bool IsRoundStart = false);

/// <summary>
/// История партии для окон «История» (<see cref="HistoryView"/>): действия каждого игрока по порядку, начала раундов,
/// конец партии. Окон может быть сколько угодно — все читают одну историю, новое окно показывает её с начала.
/// </summary>
/// <remarks>
/// За одним компьютером история пишется по действиям партии (<see cref="AttachLocal"/>), в игре через сервер —
/// по событиям обновлений (<see cref="AddEvents"/>). Текст один и тот же (<see cref="ActionText"/>): о каждой затронутой
/// карточке сказано, чья она и где лежит, о теории — кто её владелец. Скрытое (какую карту выложили взакрытую, что
/// выбрали в руке) в историю не попадает.
/// </remarks>
public sealed class GameHistory
{
  private readonly GameSession _session;
  private readonly List<HistoryEntry> _entries = [];

  public GameHistory(GameSession session)
  {
    _session = session;
  }

  /// <summary>
  /// Все записи по порядку.
  /// </summary>
  public IReadOnlyList<HistoryEntry> Entries => _entries;

  /// <summary>
  /// Добавлена запись.
  /// </summary>
  public event Action<HistoryEntry>? Added;

  /// <summary>
  /// Игра за одним компьютером: писать историю по действиям партии.
  /// </summary>
  public void AttachLocal()
  {
    _session.ActionTaken += action => Add(action.Player, ActionText.Describe(action, revealHidden: false));
    _session.AutoPassed += player => Add(player, "пасует автоматически: ходить нечем.");
    _session.Turns.FirstPlayerChanged += OnFirstPlayer;
    _session.LastRoundDeclared += column => Add(null, $"Теория «{column.Theory.Name}» дошла до финиша: этот раунд — последний.");
    _session.PhaseChanged += OnPhase;
  }

  /// <summary>
  /// Игра через сервер: записать, что произошло, по событиям обновления. Партия к этому моменту уже обновлена.
  /// </summary>
  public void AddEvents(ServerUpdate update)
  {
    foreach (var tableEvent in update.Events)
    {
      switch (tableEvent)
      {
        case FirstPlayerChanged first:
          OnFirstPlayer(PlayerAt(first.Seat));
          break;
        case PlayerActed acted:
          Add(PlayerAt(acted.Seat), acted.Text);
          break;
        case PlayerAutoPassed passed:
          Add(PlayerAt(passed.Seat), "пасует автоматически: ходить нечем.");
          break;
        case LastRoundDeclared last:
          Add(null, $"Теория «{last.Theory}» дошла до финиша: этот раунд — последний.");
          break;
        case PhaseChanged phase:
          OnPhase(phase.Phase);
          break;
      }
    }
  }

  private void Add(Player? player, string text, bool isRoundStart = false)
  {
    var entry = new HistoryEntry(_session.Round, player, text, isRoundStart);
    _entries.Add(entry);
    Added?.Invoke(entry);
  }

  private void OnFirstPlayer(Player? first) =>
    Add(null, $"Раунд {_session.Round}" + (first is null ? string.Empty : $", первым ходит {first.Name}"), isRoundStart: true);

  private void OnPhase(GamePhase phase)
  {
    if (phase == GamePhase.HeadquartersCheck)
      Add(null, "Все спасовали — конец раунда.");
    else if (phase == GamePhase.GameOver)
      Add(null, "Партия завершена.");
  }

  private Player? PlayerAt(int seat) => seat >= 0 && seat < _session.Players.Count ? _session.Players[seat] : null;
}
