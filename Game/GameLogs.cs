using ConspiracyLeague.Core.Logging;
using Godot;

namespace ConspiracyLeague.Game;

/// <summary>
/// Журналы партий на этом компьютере: папка <see cref="Directory"/> в данных игры (на Windows —
/// %APPDATA%\Godot\app_userdata\&lt;проект&gt;\game_logs), по файлу на партию.
/// </summary>
public static class GameLogs
{
  /// <summary>
  /// Папка журналов (свой путь Godot; <c>user://logs</c> занят журналом самого движка).
  /// </summary>
  public const string Directory = "user://game_logs";

  /// <summary>
  /// Открыть журнал новой партии. Скрытое в журнал на компьютере игрока не пишется (см. <see cref="GameActionLog"/>).
  /// </summary>
  /// <param name="prefix">Начало имени файла: «game» — локальная партия, «table» — стол, созданный на этом компьютере.</param>
  /// <returns>Журнал или <c>null</c>, если файл открыть не удалось — партия идёт и без журнала.</returns>
  public static GameActionLog? Open(string prefix)
  {
    try
    {
      var (log, path) = GameActionLog.OpenFile(ProjectSettings.GlobalizePath(Directory), prefix, revealHidden: false);
      GD.Print($"Журнал партии: {path}");
      return log;
    }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
    {
      GD.PrintErr($"Не удалось открыть журнал партии: {error.Message}");
      return null;
    }
  }
}
