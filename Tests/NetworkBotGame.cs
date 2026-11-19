using ConspiracyLeague.Core.Bots;
using ConspiracyLeague.Core.Commands;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Server;

namespace ConspiracyLeague.Core.Tests;

/// <summary>
/// Партия ботов на сервере (<see cref="ServerGame"/>), в которой каждый ход идёт так же, как по сети:
/// ход бота → команда → JSON → команда → сервер с местом игрока. Конец раунда сервер проводит сам.
/// </summary>
internal sealed class NetworkBotGame
{
  /// <summary>
  /// Столько неудачных ходов подряд бот пробует, потом пасует (как BotController).
  /// </summary>
  private const int MaxFailuresPerTurn = 5;

  private readonly bool _checkRejectedAreClean;

  public NetworkBotGame(int players, GameMode mode, int seed, bool checkRejectedAreClean = true)
  {
    var levels = Enum.GetValues<BotLevel>();
    Server = new ServerGame(new GameSetup(
      Enum.GetValues<PlayerColor>().Take(players)
        .Select((color, index) => new SeatSetup($"Бот {index + 1}", color, IsBot: true, levels[(seed + index) % levels.Length]))
        .ToList(),
      mode,
      FailureConsequences: true,
      KeyFrom(seed)));
    _checkRejectedAreClean = checkRejectedAreClean;
  }

  /// <summary>
  /// Воспроизводимый ключ случайности для теста.
  /// </summary>
  public static byte[] KeyFrom(int seed)
  {
    var key = new byte[SecureSeededRandom.KeyLength];
    new Random(seed).NextBytes(key);
    return key;
  }

  public ServerGame Server { get; }

  public GameSession Session => Server.Session;

  /// <summary>
  /// Отклонённые команды с причинами.
  /// </summary>
  public List<string> Rejections { get; } = [];

  /// <summary>
  /// Команды, упавшие при выполнении (<see cref="CommandResult.Fault"/>).
  /// </summary>
  public List<string> Faults { get; } = [];

  /// <summary>
  /// Отклонённые команды, которые всё же изменили партию.
  /// </summary>
  public List<string> DirtyRejections { get; } = [];

  /// <summary>
  /// Сколько команд выполнено.
  /// </summary>
  public int Accepted { get; private set; }

  /// <summary>
  /// Вызывается перед каждым ходом бота — например, чтобы забросать сервер мусорными командами.
  /// </summary>
  public Action<NetworkBotGame>? BeforeStep { get; init; }

  /// <summary>
  /// Отправить команду от игрока на месте <paramref name="seat"/> через JSON.
  /// </summary>
  public CommandResult Send(int seat, GameCommand command)
  {
    var json = CommandSerializer.Serialize(command);
    var parsed = CommandSerializer.Deserialize(json);
    Assert.That(CommandSerializer.Serialize(parsed), Is.EqualTo(json), "Команда не пережила передачу через JSON.");

    var before = _checkRejectedAreClean ? GameStateDump.ToJson(Session) : null;
    var version = Server.Version;
    var result = Server.Execute(seat, parsed);
    if (result.Fault)
      Faults.Add($"{json}: {result.Error}");
    if (!result.Accepted)
    {
      Rejections.Add($"{json}: {result.Error}");
      if (before is not null && GameStateDump.ToJson(Session) != before)
        DirtyRejections.Add($"{json}: {result.Error}");
      if (!result.Fault)
        Assert.That(Server.Version, Is.EqualTo(version), "Отклонённая команда сменила версию партии.");
    }
    else
      Accepted++;
    return result;
  }

  /// <summary>
  /// Сыграть партию до конца.
  /// </summary>
  /// <param name="maxSteps">Больше шагов партия не длится — иначе она зависла.</param>
  public void Play(int maxSteps = 20_000)
  {
    var failed = new HashSet<string>();
    (int, int, int)? turn = null;
    for (var step = 0; step < maxSteps; step++)
    {
      if (Session.Phase == GamePhase.GameOver)
        return;
      Assert.That(Session.Phase, Is.EqualTo(GamePhase.PlayerTurns), "Сервер сам проводит конец раунда.");

      var now = (Server.SeatOf(Session.CurrentPlayer), Session.ActionsLeft, Session.Round);
      if (turn != now)
      {
        failed.Clear();
        turn = now;
      }

      BeforeStep?.Invoke(this);
      if (Session.Phase == GamePhase.PlayerTurns)
        BotStep(failed);
    }

    Assert.Fail($"Партия не закончилась за {maxSteps} шагов (фаза {Session.Phase}, раунд {Session.Round}).");
  }

  private void BotStep(HashSet<string> failed)
  {
    if (Session.PendingTheoryTrigger is { } trigger)
    {
      var owner = Server.SeatOf(trigger.Owner);
      foreach (var command in CommandEncoder.FromTrigger(Session, new Bot(trigger.Owner).ChooseTrigger(Session, trigger)).ToList())
      {
        if (Session.PendingTheoryTrigger != trigger || !Send(owner, command).Accepted)
          break;
      }

      if (Session.PendingTheoryTrigger is not null && Server.ActingSeat == owner)
        Send(owner, new SkipTheoryTriggerCommand());
      return;
    }

    var seat = Server.SeatOf(Session.CurrentPlayer);
    var bot = new Bot(Session.CurrentPlayer);
    if (Session.PendingPlacement is { } choice)
    {
      if (Send(seat, CommandEncoder.FromPlacement(bot.ChoosePlacement(Session, choice))).Accepted || Session.PendingPlacement is null)
        return;
      // Как Bot.ApplyPlacement: без цели, а если без цели нельзя — первая подходящая.
      choice = Session.PendingPlacement;
      var fallback = choice.Effect.TargetIsOptional
        ? new ResolvePlacementCommand()
        : new ResolvePlacementCommand(choice.Effect.TargetCells(Session.PlacementContext(choice))
          .Where(target => target.Check.IsAllowed).Select(target => target.Cell).First());
      Assert.That(Send(seat, fallback).Accepted, Is.True, "Бот не смог выбрать цель для выложенной карточки.");
      return;
    }

    if (failed.Count >= MaxFailuresPerTurn)
    {
      Assert.That(Send(seat, new PassCommand()).Accepted, Is.True, "Пас не принят.");
      return;
    }

    var move = bot.ChooseMove(Session, failed);
    if (!Send(seat, CommandEncoder.FromMove(Session, move)).Accepted)
    {
      Assert.That(move, Is.Not.InstanceOf<PassMove>(), "Пас не принят.");
      failed.Add(move.Key);
    }
  }
}
