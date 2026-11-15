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
  private const string IncomeAnimationKey = "income_animation";
  private const string NetworkSection = "network";
  private const string PlayerNameKey = "player_name";
  private const string AddressKey = "address";
  private const string InternetAddressKey = "internet_address";
  private const string FingerprintKey = "fingerprint";
  private const string PortKey = "port";
  private const string TurnSecondsKey = "turn_seconds";

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
  /// Анимация получения денег в конце раунда: камера по очереди показывает каждую теорию и всплывающие суммы.
  /// Выключена — деньги начисляются сразу.
  /// </summary>
  public static bool IncomeAnimation { get; set; }

  /// <summary>
  /// Сетевая игра: имя игрока, последний адрес сервера, отпечаток его сертификата и порт своего стола.
  /// </summary>
  public static string PlayerName { get; set; } = "Игрок";

  /// <inheritdoc cref="PlayerName"/>
  public static string Address { get; set; } = "127.0.0.1:7777";

  /// <inheritdoc cref="PlayerName"/>
  public static string InternetAddress { get; set; } = string.Empty;

  /// <inheritdoc cref="PlayerName"/>
  public static string Fingerprint { get; set; } = string.Empty;

  /// <inheritdoc cref="PlayerName"/>
  public static int Port { get; set; } = 7777;

  /// <summary>
  /// Сетевая игра: время хода каждого игрока, с (таймер в лобби у создателя стола); 0 — без ограничения.
  /// </summary>
  public static int TurnSeconds { get; set; } = 90;

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
    config.SetValue(Section, IncomeAnimationKey, IncomeAnimation);
    config.SetValue(NetworkSection, PlayerNameKey, PlayerName);
    config.SetValue(NetworkSection, AddressKey, Address);
    config.SetValue(NetworkSection, InternetAddressKey, InternetAddress);
    config.SetValue(NetworkSection, FingerprintKey, Fingerprint);
    config.SetValue(NetworkSection, PortKey, Port);
    config.SetValue(NetworkSection, TurnSecondsKey, TurnSeconds);
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
    IncomeAnimation = config.GetValue(Section, IncomeAnimationKey, IncomeAnimation).AsBool();
    PlayerName = config.GetValue(NetworkSection, PlayerNameKey, PlayerName).AsString();
    Address = config.GetValue(NetworkSection, AddressKey, Address).AsString();
    InternetAddress = config.GetValue(NetworkSection, InternetAddressKey, InternetAddress).AsString();
    Fingerprint = config.GetValue(NetworkSection, FingerprintKey, Fingerprint).AsString();
    Port = Math.Clamp(config.GetValue(NetworkSection, PortKey, Port).AsInt32(), 1, 65535);
    TurnSeconds = Math.Clamp(config.GetValue(NetworkSection, TurnSecondsKey, TurnSeconds).AsInt32(), 0, Core.Protocol.LobbyStart.MaxTurnSeconds);
  }
}
