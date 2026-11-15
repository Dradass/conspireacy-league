using ConspiracyLeague.Core.Bots;
using ConspiracyLeague.Core.Client;
using ConspiracyLeague.Core.Commands;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Tests;

/// <summary>
/// Игрок-человек в тестах: решает ботом по своей партии-зеркалу и ходит только командами, не дожидаясь ответа
/// синхронно (ответ может прийти по сети позже).
/// </summary>
internal sealed class MirrorBot
{
  private const int MaxFailures = 5;
  private readonly HashSet<string> _failed = [];
  private long _version = -1;
  private string? _lastKey;

  public MirrorBot(GameClient client)
  {
    Client = client;
    client.Replied += reply =>
    {
      if (!reply.Accepted)
      {
        Rejections++;
        if (_lastKey is { } key)
          _failed.Add(key);
      }
    };
  }

  public GameClient Client { get; }

  public int Rejections { get; private set; }

  /// <summary>
  /// Походить, если сейчас ход этого игрока и прошлая команда уже получила ответ.
  /// </summary>
  public bool Act()
  {
    if (Decide() is not { } command)
      return false;
    Client.Send(command);
    return true;
  }

  private GameCommand? Decide()
  {
    if (Client.IsWaiting || Client.Mirror is not { Seat: { } seat } mirror)
      return null;
    var view = mirror.View;
    if (view.Phase != GamePhase.PlayerTurns || (view.PendingTrigger?.OwnerSeat ?? view.CurrentSeat) != seat)
      return null;
    if (view.Version != _version)
    {
      _failed.Clear();
      _version = view.Version;
    }

    var session = mirror.Session;
    var bot = new Bot(mirror.Player!);
    if (session.PendingTheoryTrigger is { } trigger)
    {
      _lastKey = "trigger";
      if (_failed.Count > 0)
        return new SkipTheoryTriggerCommand();
      return CommandEncoder.FromTrigger(session, bot.ChooseTrigger(session, trigger)).FirstOrDefault() ?? new SkipTheoryTriggerCommand();
    }

    if (session.PendingPlacement is { } choice)
    {
      _lastKey = "placement";
      if (_failed.Count == 0)
        return CommandEncoder.FromPlacement(bot.ChoosePlacement(session, choice));
      var placement = view.PendingPlacement!;
      return new ResolvePlacementCommand(placement.TargetIsOptional ? null : placement.AllowedTargets?.FirstOrDefault());
    }

    if (_failed.Count >= MaxFailures)
    {
      _lastKey = null;
      return new PassCommand();
    }

    var move = bot.ChooseMove(session, _failed);
    _lastKey = move is PassMove ? "pass" : move.Key;
    return CommandEncoder.FromMove(session, move);
  }
}
