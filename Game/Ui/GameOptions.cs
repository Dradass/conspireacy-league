using Godot;

namespace ConspiracyLeague.Game.Ui;

/// <summary>
/// Опции игры из окна «Опции» стартового меню. Сохраняются в папке данных Godot (<see cref="FilePath"/>)
/// и действуют для всех следующих партий.
/// </summary>
public static class GameOptions
{
  /// <summary>
  /// Куда сохраняются опции.
  /// </summary>
  public const string FilePath = "user://options.cfg";

  private const string Section = "game";
  private const string RadialFieldKey = "radial_field";
  private const string CriticalFailureKey = "critical_failure";

  static GameOptions() => Load();

  /// <summary>
  /// Вид поля: круг (радиальное поле) или классический (сетка).
  /// </summary>
  public static bool RadialField { get; set; }

  /// <summary>
  /// Критический провал: на низких гранях кубика проваленный бездомный уходит из игры, а гражданин становится бездомным.
  /// </summary>
  public static bool CriticalFailure { get; set; } = true;

  /// <summary>
  /// Сохранить опции на диск.
  /// </summary>
  public static void Save()
  {
    var config = new ConfigFile();
    config.SetValue(Section, RadialFieldKey, RadialField);
    config.SetValue(Section, CriticalFailureKey, CriticalFailure);
    var error = config.Save(FilePath);
    if (error != Error.Ok)
      GD.PrintErr($"Не удалось сохранить опции в {FilePath}: {error}");
  }

  private static void Load()
  {
    var config = new ConfigFile();
    if (config.Load(FilePath) != Error.Ok)
      return;

    RadialField = config.GetValue(Section, RadialFieldKey, RadialField).AsBool();
    CriticalFailure = config.GetValue(Section, CriticalFailureKey, CriticalFailure).AsBool();
  }
}
