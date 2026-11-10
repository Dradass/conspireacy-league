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
  private const string BotSpeedKey = "bot_speed";
  private const string FollowBotsKey = "follow_bots";
  private const string RoundSummaryKey = "round_summary_view";

  /// <summary>
  /// Во сколько раз медленнее исходной скорости ходят боты на каждом делении ползунка «Скорость ботов»:
  /// от исходной скорости до в 4 раза медленнее.
  /// </summary>
  public static readonly double[] BotSlowdowns = [1, 4.0 / 3, 5.0 / 3, 2, 2.5, 3, 3.5, 4];

  /// <summary>
  /// Деление ползунка по умолчанию — боты ходят в 2 раза медленнее исходной скорости.
  /// </summary>
  public const int DefaultBotSpeed = 3;

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
  /// Деление ползунка «Скорость ботов» (индекс в <see cref="BotSlowdowns"/>): 0 — быстрее всего.
  /// </summary>
  public static int BotSpeed { get; set; } = DefaultBotSpeed;

  /// <summary>
  /// Во сколько раз медленнее исходной скорости ходят боты (по <see cref="BotSpeed"/>).
  /// </summary>
  public static double BotSlowdown => BotSlowdowns[Math.Clamp(BotSpeed, 0, BotSlowdowns.Length - 1)];

  /// <summary>
  /// Камера следует за действиями ботов: куда бот кладёт карточку, какую карточку играет и на какую цель.
  /// </summary>
  public static bool FollowBots { get; set; }

  /// <summary>
  /// Как показывать итоги конца раунда (доход от адептов): текстом или столбчатой диаграммой.
  /// </summary>
  public static RoundSummaryView RoundSummary { get; set; } = RoundSummaryView.Text;

  /// <summary>
  /// Сохранить опции на диск.
  /// </summary>
  public static void Save()
  {
    var config = new ConfigFile();
    config.SetValue(Section, RadialFieldKey, RadialField);
    config.SetValue(Section, CriticalFailureKey, CriticalFailure);
    config.SetValue(Section, BotSpeedKey, BotSpeed);
    config.SetValue(Section, FollowBotsKey, FollowBots);
    config.SetValue(Section, RoundSummaryKey, (int)RoundSummary);
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
    BotSpeed = Math.Clamp(config.GetValue(Section, BotSpeedKey, BotSpeed).AsInt32(), 0, BotSlowdowns.Length - 1);
    FollowBots = config.GetValue(Section, FollowBotsKey, FollowBots).AsBool();
    var summary = config.GetValue(Section, RoundSummaryKey, (int)RoundSummary).AsInt32();
    RoundSummary = Enum.IsDefined(typeof(RoundSummaryView), summary) ? (RoundSummaryView)summary : RoundSummaryView.Text;
  }
}

/// <summary>
/// Вид итогов конца раунда.
/// </summary>
public enum RoundSummaryView
{
  /// <summary>
  /// Текстом: строка на теорию с доходом каждого игрока.
  /// </summary>
  Text,

  /// <summary>
  /// Столбчатой диаграммой: столбец на теорию, сложенный из столбиков игроков.
  /// </summary>
  BarChart,
}
