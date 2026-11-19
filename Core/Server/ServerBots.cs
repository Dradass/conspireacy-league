using ConspiracyLeague.Core.Bots;
using ConspiracyLeague.Core.Commands;
using ConspiracyLeague.Core.Game;

namespace ConspiracyLeague.Core.Server;

/// <summary>
/// Ходы ботов на сервере. Бот выбирает ход, ход превращается в команду и выполняется через <see cref="ServerGame.Execute"/>
/// — с теми же проверками, что и ходы людей. Так же, как в интерфейсе (BotController): неудавшийся ход до конца
/// действия больше не предлагается, после нескольких неудач бот пасует.
/// </summary>
/// <remarks>
/// Боты видят всю партию, как и в одиночной игре: они работают на сервере и ничего никому не отправляют.
/// </remarks>
internal sealed class ServerBots(ServerGame game)
{
  /// <summary>
  /// Столько неудачных ходов подряд бот пробует в одном действии, потом пасует.
  /// </summary>
  private const int MaxFailuresPerAction = 5;

  /// <summary>
  /// Столько шагов подряд без изменений партии — значит, бот застрял; дальше он не ходит.
  /// </summary>
  private const int MaxIdleSteps = 20;

  private readonly HashSet<string> _failed = [];
  private (int Seat, int Actions, int Round, long Version)? _action;
  private long _lastVersion = -1;
  private int _idleSteps;

  /// <summary>
  /// Застрял ли бот: несколько шагов подряд ни одна его команда не принята.
  /// </summary>
  public bool IsStuck => _idleSteps >= MaxIdleSteps;

  public bool Step()
  {
    if (game.IsBroken || game.ActingSeat is not { } seat || !game.IsBotSeat(seat))
      return false;

    if (game.Version == _lastVersion)
    {
      if (++_idleSteps >= MaxIdleSteps)
        return false;
    }
    else
      _idleSteps = 0;
    _lastVersion = game.Version;

    var session = game.Session;
    var player = session.Players[seat];
    var bot = new Bot(player);
    try
    {
      if (session.PendingTheoryTrigger is { } trigger)
        RespondToTrigger(seat, bot, trigger);
      else if (session.PendingPlacement is { } choice)
        RespondToPlacement(seat, bot, choice);
      else
        Move(seat, bot);
    }
    catch (Exception error) when (error is InvalidOperationException or ArgumentException or NotImplementedException)
    {
      // Бот не смог выбрать ход (ошибка в его оценке) — не ломаем партию: пасуем за него.
      if (session.CheckNoPending().IsAllowed)
        game.Execute(seat, new PassCommand());
    }

    return true;
  }

  private void RespondToTrigger(int seat, Bot bot, Field.TheoryTrigger trigger)
  {
    foreach (var command in CommandEncoder.FromTrigger(game.Session, bot.ChooseTrigger(game.Session, trigger)).ToList())
    {
      if (game.Session.PendingTheoryTrigger != trigger || !game.Execute(seat, command).Accepted)
        break;
    }

    // Выбор не принят или не закончен — отказаться от действия основной теории.
    if (game.Session.PendingTheoryTrigger is not null && game.ActingSeat == seat)
      game.Execute(seat, new SkipTheoryTriggerCommand());
  }

  private void RespondToPlacement(int seat, Bot bot, PlacementChoice choice)
  {
    if (game.Execute(seat, CommandEncoder.FromPlacement(bot.ChoosePlacement(game.Session, choice))).Accepted
      || game.Session.PendingPlacement is not { } pending)
      return;

    // Как Bot.ApplyPlacement: без цели, а если без цели нельзя — первая подходящая.
    var effect = pending.Effect;
    var fallback = effect.TargetIsOptional
      ? new ResolvePlacementCommand()
      : new ResolvePlacementCommand(effect.TargetCells(game.Session.PlacementContext(pending))
        .Where(target => target.Check.IsAllowed).Select(target => (Field.Cell?)target.Cell).FirstOrDefault());
    game.Execute(seat, fallback);
  }

  private void Move(int seat, Bot bot)
  {
    var session = game.Session;
    var action = (seat, session.ActionsLeft, session.Round, game.Version);
    // Новое действие — неудачи прошлого не в счёт. Версия меняется с каждым выполненным ходом.
    if (_action is not { } previous || previous.Seat != action.seat || previous.Actions != action.ActionsLeft
      || previous.Round != action.Round || previous.Version != action.Version)
      _failed.Clear();
    _action = action;

    if (_failed.Count >= MaxFailuresPerAction)
    {
      game.Execute(seat, new PassCommand());
      return;
    }

    var move = bot.ChooseMove(session, _failed);
    var result = game.Execute(seat, CommandEncoder.FromMove(session, move));
    if (!result.Accepted)
      _failed.Add(move is PassMove ? "pass" : move.Key);
  }
}
