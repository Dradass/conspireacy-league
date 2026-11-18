using ConspiracyLeague.Core.Bots;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Tests;

/// <summary>
/// Партии ботов между собой без сервера и анимаций: чтобы сравнивать уровни и подбирать веса (<see cref="BotProfile"/>).
/// </summary>
internal static class BotArena
{
  /// <summary>
  /// Итог одного участника за все партии.
  /// </summary>
  public sealed class Entry(string name)
  {
    public string Name { get; } = name;
    public int Games { get; set; }
    public double Wins { get; set; }
    public long Score { get; set; }
    public long Leader { get; set; }
    public long Adept { get; set; }
    public long Theories { get; set; }
    public long MoneyPoints { get; set; }
    public long Money { get; set; }
    public long OwnedActive { get; set; }
    public long OwnedInactive { get; set; }
    public long OwnedClosed { get; set; }
    public long WastedAdepts { get; set; }

    public override string ToString() =>
      $"{Name,-10} побед {Wins / Games,6:P0}  очки {(double)Score / Games,6:F1}  (влияние {(double)Leader / Games,5:F1}, адепты {(double)Adept / Games,5:F1}, "
      + $"теорий {(double)Theories / Games,4:F2}, за $ {(double)MoneyPoints / Games,5:F1})  $ {(double)Money / Games,6:F1}"
      + $"  владеет: действ. {(double)OwnedActive / Games:F2}, недейств. {(double)OwnedInactive / Games:F2}, закр. {(double)OwnedClosed / Games:F2}, адепты впустую {(double)WastedAdepts / Games:F1}";
  }

  /// <summary>
  /// Сыграть <paramref name="games"/> партий: участники садятся по кругу на разные места.
  /// </summary>
  public static IReadOnlyList<Entry> Play(IReadOnlyList<(string Name, BotProfile Profile)> seats, GameMode mode, int games, int seed = 1,
    TextWriter? log = null, bool revealHiddenAtEnd = true)
  {
    var entries = seats.Select(seat => new Entry(seat.Name)).ToList();
    for (var game = 0; game < games; game++)
    {
      var shift = game % seats.Count;
      var order = Enumerable.Range(0, seats.Count).Select(i => (i + shift) % seats.Count).ToList();
      var players = order.Select(index => new Player(seats[index].Name, (PlayerColor)order.IndexOf(index), GameSession.StartingMoney) { IsBot = true }).ToList();
      var bots = players.Select((player, i) => new Bot(player, seats[order[i]].Profile)).ToDictionary(bot => bot.Player);
      var session = new GameSession(players, new Random(seed * 100_003 + game), mode);
      session.Field.FailureConsequences = true;
      session.Field.RevealHiddenAtEnd = revealHiddenAtEnd;
      using var actionLog = log is null ? null : new Logging.GameActionLog(log, revealHidden: true);
      actionLog?.Attach(session);
      session.Start();
      Run(session, bots);
      if (log is not null)
      {
        foreach (var column in session.Field.TheoryColumns)
          log.WriteLine($"Теория {column.Theory.Name}: владелец {column.Owner.Name}, лидеров {column.Leaders}, влияние {column.Influence}, "
            + $"адептов {column.Adepts}, действующая {column.IsActive}, закрыта {column.IsClosed}, лидеры: "
            + string.Join(", ", column.Members.Select(member => $"{member.Card.Name}/{member.Owner.Name}")));
        foreach (var player in players)
          log.WriteLine($"{player.Name}: {session.ScoreOf(player)}");
      }

      var winners = session.Winners;
      for (var i = 0; i < players.Count; i++)
      {
        var entry = entries[order[i]];
        var player = players[i];
        entry.Games++;
        if (winners.Contains(player))
          entry.Wins += 1.0 / winners.Count;
        entry.Money += player.Money;
        foreach (var column in session.Field.TheoryColumns.Where(column => column.Owner == player))
        {
          if (column.IsClosed)
            entry.OwnedClosed++;
          else if (column.IsActive)
            entry.OwnedActive++;
          else
            entry.OwnedInactive++;
          if (!column.IsActive)
            entry.WastedAdepts += column.Adepts;
        }
        if (mode == GameMode.Race)
        {
          entry.Score += session.RaceScoreOf(player).Total;
          continue;
        }
        var score = session.ScoreOf(player);
        entry.Score += mode == GameMode.Money ? player.Money : score.Total;
        entry.Leader += score.LeaderPoints;
        entry.Adept += score.AdeptPoints;
        entry.Theories += score.TheoryCount;
        entry.MoneyPoints += score.MoneyPoints;
      }
    }
    return entries;
  }

  private static void Run(GameSession session, IReadOnlyDictionary<Player, Bot> bots)
  {
    var failed = new HashSet<string>();
    (Player, int, int)? action = null;
    for (var step = 0; step < 50_000; step++)
    {
      if (session.Phase == GamePhase.GameOver)
        return;
      if (session.Phase == GamePhase.HeadquartersCheck)
      {
        RoundEnd.Run(session);
        continue;
      }

      if (session.PendingTheoryTrigger is { } trigger)
      {
        bots[trigger.Owner].RespondToPending(session);
        continue;
      }

      var bot = bots[session.CurrentPlayer];
      if (session.PendingPlacement is not null)
      {
        bot.RespondToPending(session);
        continue;
      }

      var current = (session.CurrentPlayer, session.ActionsLeft, session.Round);
      if (action != current)
        failed.Clear();
      action = current;
      if (failed.Count >= 5)
      {
        session.Pass();
        continue;
      }

      var move = bot.ChooseMove(session, failed);
      try
      {
        bot.Execute(session, move);
        failed.Clear();
        action = null;
      }
      catch (Exception error) when (error is InvalidOperationException or ArgumentException)
      {
        failed.Add(move is PassMove ? "pass" : move.Key);
      }
    }
    throw new InvalidOperationException("Партия ботов не закончилась.");
  }
}

/// <summary>
/// Турниры ботов — запускаются вручную: долгие, нужны для подбора весов. Переменные среды: ARENA_GAMES — число партий,
/// ARENA_MODE — режим, ARENA_SEED — набор партий, ARENA_VARIANT — веса претендента (<see cref="Variant"/>),
/// ARENA_LOG — куда записать журнал одной партии.
/// </summary>
/// <example>
/// <c>ARENA_GAMES=160 dotnet test Tests -c Release --filter "FullyQualifiedName~BotArenaTests.Challenger" --logger "console;verbosity=detailed"</c>
/// </example>
[Explicit, Category("BotArena")]
public sealed class BotArenaTests
{
  private static int Games => int.TryParse(Environment.GetEnvironmentVariable("ARENA_GAMES"), out var games) ? games : 40;

  private static int Seed => int.TryParse(Environment.GetEnvironmentVariable("ARENA_SEED"), out var seed) ? seed : 1;

  private static string VariantName => Environment.GetEnvironmentVariable("ARENA_VARIANT") ?? "Ultra";

  private static GameMode Mode =>
    Enum.TryParse<GameMode>(Environment.GetEnvironmentVariable("ARENA_MODE"), out var mode) ? mode : GameMode.TheoryDevelopment;

  /// <summary>
  /// Ультра бот до разбора партий против людей (ноябрь 2026): завышал розыгрыши личного штаба, грубо проверял
  /// состоятельность штабов и охотнее входил в чужие теории. Соперник для проверки новых весов.
  /// </summary>
  private static BotProfile PreviousUltra =>
    BotProfile.Ultra with
    {
      ByMode = new Dictionary<GameMode, BotProfile>(BotProfile.Ultra.ByMode!)
      {
        [GameMode.TheoryDevelopment] = BotProfile.Ultra.ForMode(GameMode.TheoryDevelopment) with
        {
          UsesFactor = BotProfile.Hard.ForMode(GameMode.TheoryDevelopment).UsesFactor,
          InsolvencyCare = 0,
          SolvencyPlay = 0,
          SabotageFactor = 1,
          JoinGiftFactor = BotProfile.Hard.ForMode(GameMode.TheoryDevelopment).JoinGiftFactor,
        },
      },
    };

  /// <summary>
  /// Веса претендента по имени: сюда добавляются варианты для проверки.
  /// </summary>
  private static BotProfile Variant(string name) => name switch
  {
    "Previous" => PreviousUltra,
    "Sabotage1" => BotProfile.Ultra with
    {
      ByMode = new Dictionary<GameMode, BotProfile>(BotProfile.Ultra.ByMode!)
      {
        [GameMode.TheoryDevelopment] = BotProfile.Ultra.ForMode(GameMode.TheoryDevelopment) with { SabotageFactor = 1 },
      },
    },
    _ => BotProfile.Ultra,
  };

  private static IReadOnlyList<(string, BotProfile)> ChallengerSeats =>
    [(VariantName, Variant(VariantName)), ("Medium", BotProfile.Medium), ("Hard", BotProfile.Hard), ("Previous", PreviousUltra)];

  [Test]
  public void Levels()
  {
    var watch = System.Diagnostics.Stopwatch.StartNew();
    var entries = BotArena.Play(
      [("Easy", BotProfile.Easy), ("Medium", BotProfile.Medium), ("Hard", BotProfile.Hard), ("Ultra", BotProfile.Ultra)],
      Mode, Games, Seed);
    foreach (var entry in entries)
      TestContext.Progress.WriteLine(entry);
    TestContext.Progress.WriteLine($"{Mode}: {Games} партий за {watch.Elapsed.TotalSeconds:F0} с");
  }

  [Test]
  public void Challenger()
  {
    foreach (var entry in BotArena.Play(ChallengerSeats, Mode, Games, Seed))
      TestContext.Progress.WriteLine(entry);
  }

  [Test]
  public void LoggedGame()
  {
    var path = Environment.GetEnvironmentVariable("ARENA_LOG") ?? Path.Combine(Path.GetTempPath(), "arena.log");
    using var writer = new StreamWriter(path);
    BotArena.Play(ChallengerSeats, Mode, 1, Seed, writer);
  }
}
