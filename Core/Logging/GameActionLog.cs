using ConspiracyLeague.Core.Cards;
using ConspiracyLeague.Core.Field;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Logging;

/// <summary>
/// Журнал партии: действие каждого игрока (людей и ботов), начало раундов, автоматические пасы, итог партии — по строке
/// с временем, раундом и изменением денег. Пишется в текстовый файл (или любой <see cref="TextWriter"/>).
/// </summary>
/// <remarks>
/// <para>
/// Скрытое (какую карту выложили взакрытую, какую карточку в руке выбрал владелец основной теории) пишется, только если
/// журнал ведёт сервер, к которому у игроков нет доступа (<c>revealHidden</c>). Журнал на компьютере игрока его не пишет:
/// иначе, открыв файл посреди партии, можно было бы подсмотреть чужие карты.
/// </para>
/// <para>
/// Потокобезопасен: строки пишутся под блокировкой и сразу сбрасываются на диск — журнал не теряется при сбое.
/// </para>
/// </remarks>
public sealed class GameActionLog : IDisposable
{
  private readonly object _lock = new();
  private readonly TextWriter _writer;
  private readonly bool _revealHidden;
  private readonly Func<DateTime> _clock;
  private GameSession? _session;
  private bool _disposed;

  /// <param name="writer">Куда писать. Журнал закрывает его при <see cref="Dispose"/>.</param>
  /// <param name="revealHidden">Писать скрытое (см. описание класса).</param>
  /// <param name="clock">Текущее время; по умолчанию — местное.</param>
  public GameActionLog(TextWriter writer, bool revealHidden, Func<DateTime>? clock = null)
  {
    ArgumentNullException.ThrowIfNull(writer);
    _writer = writer;
    _revealHidden = revealHidden;
    _clock = clock ?? (() => DateTime.Now);
  }

  /// <summary>
  /// Открыть журнал в новом файле папки <paramref name="directory"/>: <c>{prefix}-ГГГГММДД-ччммсс.log</c>, UTF-8.
  /// </summary>
  /// <returns>Журнал и путь к файлу.</returns>
  public static (GameActionLog Log, string Path) OpenFile(string directory, string prefix, bool revealHidden)
  {
    Directory.CreateDirectory(directory);
    var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
    var path = System.IO.Path.Combine(directory, $"{prefix}-{stamp}.log");
    for (var n = 2; File.Exists(path); n++)
      path = System.IO.Path.Combine(directory, $"{prefix}-{stamp}-{n}.log");
    var writer = new StreamWriter(path, append: false, new System.Text.UTF8Encoding(false)) { AutoFlush = true };
    return (new GameActionLog(writer, revealHidden), path);
  }

  /// <summary>
  /// Вести журнал этой партии. Если журнал уже следил за другой партией (сервер пересобрал её после сбоя),
  /// он переключается на новую.
  /// </summary>
  public void Attach(GameSession session)
  {
    ArgumentNullException.ThrowIfNull(session);
    lock (_lock)
    {
      var first = _session is null;
      Detach();
      _session = session;
      session.ActionTaken += OnAction;
      session.AutoPassed += OnAutoPassed;
      session.Turns.FirstPlayerChanged += OnFirstPlayer;
      session.PhaseChanged += OnPhase;
      session.LastRoundDeclared += OnLastRound;
      if (first)
        WriteHeader(session);
    }
  }

  /// <summary>
  /// Записать строку не о действии игрока (например, кто подключился к серверу).
  /// </summary>
  public void Write(string text) => Line(null, text);

  public void Dispose()
  {
    lock (_lock)
    {
      if (_disposed)
        return;
      _disposed = true;
      Detach();
      _writer.Dispose();
    }
  }

  private void Detach()
  {
    if (_session is not { } session)
      return;
    session.ActionTaken -= OnAction;
    session.AutoPassed -= OnAutoPassed;
    session.Turns.FirstPlayerChanged -= OnFirstPlayer;
    session.PhaseChanged -= OnPhase;
    session.LastRoundDeclared -= OnLastRound;
  }

  private void WriteHeader(GameSession session)
  {
    Line(null, $"Партия: режим «{ModeName(session.Mode)}», игроков {session.Players.Count}"
      + (session.Field.FailureConsequences ? ", последствия провала включены." : ", без последствий провала."));
    foreach (var player in session.Players)
      Line(null, $"  {player.Name} ({player.Color}){(player.IsBot ? $", бот ({player.BotLevel})" : string.Empty)}, {player.Money}$");
    // Журнал подключили к уже начатой партии (на сервере партия начинается при создании): сразу отмечаем раунд.
    if (session.Round > 0)
      OnFirstPlayer(session.Turns.FirstPlayer);
  }

  private void OnAction(PlayerAction action) => Line(action.Player, ActionText.Describe(action, _revealHidden, alwaysShowMoney: true));

  private void OnAutoPassed(Player player) => Line(player, "автоматический пас: ходить нечем.");

  private void OnFirstPlayer(Player player) =>
    Line(null, $"— Раунд {_session?.Round}. Первым ходит {player.Name}. Деньги: "
      + string.Join(", ", _session?.Players.Select(other => $"{other.Name} {other.Money}$") ?? []) + " —");

  private void OnLastRound(TheoryColumn column) => Line(null, $"Теория «{column.Theory.Name}» дошла до финиша: раунд последний.");

  private void OnPhase(GamePhase phase)
  {
    if (_session is not { } session)
      return;
    if (phase == GamePhase.HeadquartersCheck)
      Line(null, "Все спасовали — конец раунда.");
    if (phase != GamePhase.GameOver)
      return;

    Line(null, "Партия завершена. Итог:");
    var place = 1;
    foreach (var player in session.Ranking)
    {
      var score = session.Mode switch
      {
        GameMode.TheoryDevelopment => $"{session.ScoreOf(player).Total} очков, {player.Money}$",
        GameMode.Race => $"{session.RaceScoreOf(player).Total} очков, {player.Money}$",
        _ => $"{player.Money}$",
      };
      Line(null, $"  {place++}. {player.Name} — {score}");
    }
  }

  private static string ModeName(GameMode mode) => mode switch
  {
    GameMode.Money => "Заработок денег",
    GameMode.TheoryDevelopment => "Развитие теорий",
    GameMode.Race => "Гонка",
    _ => mode.ToString(),
  };

  private void Line(Player? player, string text)
  {
    lock (_lock)
    {
      if (_disposed)
        return;
      var round = _session is { Round: > 0 } session ? $"Р{session.Round}" : "--";
      var who = player is null ? string.Empty : $"{player.Name}: ";
      try
      {
        _writer.WriteLine($"{_clock():yyyy-MM-dd HH:mm:ss} {round,-4} {who}{text}");
      }
      catch (IOException)
      {
        // Диск переполнен или файл недоступен: журнал не должен ронять партию.
      }
    }
  }
}
