using ConspiracyLeague.Core.Bots;
using ConspiracyLeague.Core.Commands;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Views;

namespace ConspiracyLeague.Core.Server;

/// <summary>
/// Игрок за столом при создании партии.
/// </summary>
public sealed record SeatSetup(string Name, PlayerColor Color, bool IsBot = false, BotLevel BotLevel = BotLevel.Easy);

/// <summary>
/// Всё, из чего партия собирается заново: игроки, режим, правила и секретный ключ случайности.
/// </summary>
/// <param name="Seats">Игроки в порядке мест за столом.</param>
/// <param name="Mode">Режим партии.</param>
/// <param name="FailureConsequences">Последствия провала проверки успеха (<see cref="Field.PlayingField.FailureConsequences"/>).</param>
/// <param name="RandomKey">
/// Секретный ключ случайности (<see cref="SecureSeededRandom"/>). Никогда не покидает сервер: по нему можно узнать
/// колоду и все будущие броски.
/// </param>
public sealed record GameSetup(IReadOnlyList<SeatSetup> Seats, GameMode Mode, bool FailureConsequences, byte[] RandomKey)
{
  /// <summary>
  /// Партия с новым случайным ключом.
  /// </summary>
  public static GameSetup Create(IReadOnlyList<SeatSetup> seats, GameMode mode = GameMode.TheoryDevelopment, bool failureConsequences = true) =>
    new(seats, mode, failureConsequences, SecureSeededRandom.NewKey());
}

/// <summary>
/// Выполненная команда в журнале партии.
/// </summary>
/// <param name="Seat">Место игрока, который её прислал.</param>
/// <param name="Command">Команда.</param>
/// <param name="Locators">Где лежали карточки, на которые она ссылалась (<see cref="PreparedCommand.Locators"/>).</param>
public sealed record LoggedCommand(int Seat, GameCommand Command, IReadOnlyDictionary<int, CardLocator> Locators);

/// <summary>
/// Партия на сервере: единственное место, где меняется её состояние. Принимает команды игроков, сам проводит
/// конец раунда и гарантирует, что команда либо выполнена целиком, либо партия осталась как была.
/// </summary>
/// <remarks>
/// <para>
/// Каждая команда сначала проверяется, ничего не меняя (<see cref="CommandProcessor.Prepare"/>). Если проверенная
/// команда всё же упала при выполнении (ошибка в коде правил), партия собирается заново из <see cref="Setup"/> и
/// журнала выполненных команд: случайность воспроизводится по секретному ключу, поэтому новая партия совпадает
/// со старой до этой команды. Идентификаторы карточек после этого новые — клиенты получают состояние целиком.
/// </para>
/// <para>
/// Класс не потокобезопасен: команды одной партии сервер выполняет по очереди.
/// </para>
/// </remarks>
public sealed class ServerGame
{
  private readonly List<LoggedCommand> _log = [];
  private readonly List<TableEvent> _events = [];
  private TableEventRecorder _recorder;
  private readonly ServerBots _bots;

  /// <param name="setup">Игроки, режим и ключ случайности.</param>
  public ServerGame(GameSetup setup)
  {
    ArgumentNullException.ThrowIfNull(setup);
    if (setup.Seats.Count == 0)
      throw new ArgumentException("За столом нет игроков.", nameof(setup));
    Setup = setup;
    (Session, Processor) = Build();
    _recorder = new TableEventRecorder(Session);
    _bots = new ServerBots(this);
  }

  /// <summary>
  /// Из чего собрана партия.
  /// </summary>
  public GameSetup Setup { get; }

  /// <summary>
  /// Текущая партия. После отката (<see cref="CommandResult.Fault"/>) это новый объект.
  /// </summary>
  public GameSession Session { get; private set; }

  internal CommandProcessor Processor { get; private set; }

  /// <summary>
  /// Версия состояния: растёт с каждым изменением партии (выполненная команда, откат). По ней клиент понимает,
  /// что его состояние устарело.
  /// </summary>
  public long Version { get; private set; }

  /// <summary>
  /// Партию больше нельзя продолжать: после сбоя её не удалось собрать заново. Команды не принимаются.
  /// </summary>
  public bool IsBroken { get; private set; }

  /// <summary>
  /// Журнал выполненных команд — по нему партию можно повторить.
  /// </summary>
  public IReadOnlyList<LoggedCommand> Log => _log;

  /// <summary>
  /// Сбой при выполнении команды или конце раунда — для журнала сервера: команда, место игрока и ошибка.
  /// </summary>
  public event Action<int, GameCommand, Exception>? Faulted;

  /// <summary>
  /// Место игрока, от которого сейчас ждут команду: владелец основной теории, если ждёт её выбор, иначе тот, кто
  /// ходит; <c>null</c>, если партия завершена.
  /// </summary>
  public int? ActingSeat =>
    Session.Phase != GamePhase.PlayerTurns ? null
    : SeatOf(Session.PendingTheoryTrigger?.Owner ?? Session.CurrentPlayer);

  /// <summary>
  /// Партия глазами игрока на месте <paramref name="seat"/> (или зрителя — <c>null</c>): только то, что он вправе знать.
  /// Это единственное, что сервер отправляет клиентам о партии.
  /// </summary>
  public PlayerView ViewFor(int? seat) => PlayerViews.For(Session, seat, Version);

  /// <summary>
  /// Забрать события стола, накопленные с прошлого раза: их сервер рассылает всем вместе с видом партии
  /// (<see cref="UpdateFor"/>). События одинаковы для всех игроков — скрытого в них нет.
  /// </summary>
  public IReadOnlyList<TableEvent> DrainEvents()
  {
    var drained = _events.ToList();
    _events.Clear();
    return drained;
  }

  /// <summary>
  /// Сообщение игроку на месте <paramref name="seat"/> (или зрителю): события и его вид партии.
  /// </summary>
  /// <param name="seat">Место игрока или <c>null</c> — зритель.</param>
  /// <param name="events">События из <see cref="DrainEvents"/> — одни и те же для всех получателей.</param>
  public ServerUpdate UpdateFor(int? seat, IReadOnlyList<TableEvent> events) => new(Version, events, ViewFor(seat));

  /// <summary>
  /// Сделать ход за бота, от которого сейчас ждут команду (<see cref="ActingSeat"/>), — через те же проверки, что
  /// и ходы людей.
  /// </summary>
  /// <returns><c>false</c>, если сейчас ход не бота (или партия завершена).</returns>
  public bool RunBotStep() => _bots.Step();

  /// <summary>
  /// Ходить за ботов, пока не дойдёт очередь человека или партия не закончится.
  /// </summary>
  /// <param name="maxSteps">Больше шагов подряд не делать — защита от зацикливания.</param>
  /// <returns>Сколько шагов сделано.</returns>
  public int RunBots(int maxSteps = 10_000)
  {
    var steps = 0;
    while (steps < maxSteps && RunBotStep())
      steps++;
    return steps;
  }

  /// <summary>
  /// Бот застрял: несколько шагов подряд ни одна его команда не принята. Ходы ботов остановлены — это ошибка в боте
  /// или правилах, её стоит записать в журнал сервера.
  /// </summary>
  public bool BotsStuck => _bots.IsStuck;

  /// <summary>
  /// Сидит ли за местом бот.
  /// </summary>
  public bool IsBotSeat(int seat) => seat >= 0 && seat < Setup.Seats.Count && Setup.Seats[seat].IsBot;

  /// <summary>
  /// Место игрока за столом.
  /// </summary>
  public int SeatOf(Player player)
  {
    for (var i = 0; i < Session.Players.Count; i++)
    {
      if (Session.Players[i] == player)
        return i;
    }

    throw new ArgumentException($"Игрока {player} нет за столом.", nameof(player));
  }

  /// <summary>
  /// Выполнить команду игрока. Неверная команда отклоняется, ничего не меняя; если же команда упала при выполнении,
  /// партия откатывается к состоянию до неё (<see cref="CommandResult.Fault"/>).
  /// </summary>
  /// <param name="seat">Место игрока — по соединению, от которого пришла команда.</param>
  /// <param name="command">Команда.</param>
  public CommandResult Execute(int seat, GameCommand? command)
  {
    if (IsBroken)
      return CommandResult.Rejected("Партия остановлена из-за внутренней ошибки.");

    PreparedCommand prepared;
    try
    {
      prepared = Processor.Prepare(seat, command);
    }
    catch (CommandRejectedException error)
    {
      return CommandResult.Rejected(error.Message);
    }

    SuccessCheck? check;
    try
    {
      if (IntentOf(prepared) is { } intent)
        _recorder.Add(intent);
      check = prepared.Run();
      FaultInjector?.Invoke(Session);
      FinishRounds();
    }
    catch (Exception error)
    {
      Faulted?.Invoke(seat, command!, error);
      // События упавшей команды описывают отменённые изменения — вместо них клиенты просто получат новый вид.
      _recorder.Drain();
      _events.Add(new StateReset());
      Rollback();
      return new CommandResult(false, IsBroken
        ? "Партия остановлена из-за внутренней ошибки."
        : "Внутренняя ошибка: команда отменена, партия возвращена к состоянию до неё.", Fault: true);
    }

    _log.Add(new LoggedCommand(seat, prepared.Command, prepared.Locators));
    _events.AddRange(_recorder.Drain());
    Version++;
    return CommandResult.Ok(check);
  }

  /// <summary>
  /// Только для тестов: вызывается после выполнения команды, чтобы смоделировать сбой посреди неё.
  /// </summary>
  internal Action<GameSession>? FaultInjector { get; set; }

  /// <summary>
  /// Собрать партию заново из <see cref="Setup"/> и журнала — такой, какой она должна быть сейчас.
  /// </summary>
  /// <exception cref="InvalidOperationException">Журнал не повторяется — партия недетерминирована.</exception>
  public GameSession Replay() => Replay(_log).Session;

  private (GameSession Session, CommandProcessor Processor) Replay(IReadOnlyList<LoggedCommand> log)
  {
    var (session, processor) = Build();
    for (var i = 0; i < log.Count; i++)
    {
      var entry = log[i];
      try
      {
        processor.Prepare(entry.Seat, entry.Command, entry.Locators).Run();
        FinishRounds(session);
      }
      catch (Exception error)
      {
        throw new InvalidOperationException($"Команда №{i + 1} журнала не повторяется: {error.Message}", error);
      }
    }

    return (session, processor);
  }

  /// <summary>
  /// После сбоя партия пересобрана заново (<see cref="Session"/> — новый объект); аргумент — новая партия.
  /// </summary>
  public event Action<GameSession>? SessionReplaced;

  private void Rollback()
  {
    try
    {
      (Session, Processor) = Replay(_log);
      _recorder = new TableEventRecorder(Session);
      SessionReplaced?.Invoke(Session);
    }
    catch (InvalidOperationException)
    {
      IsBroken = true;
    }

    Version++;
  }

  /// <summary>
  /// Что игрок сделал командой — событие для анимации, которого нет среди событий партии.
  /// </summary>
  private TableEvent? IntentOf(PreparedCommand prepared) => prepared.Command switch
  {
    BuyCommand buy => new CardBought(prepared.Seat, buy.Cell),
    PlaceCommand place => new CardPlaced(prepared.Seat, place.Cell),
    PlaceInWallCommand wall => new CardPlacedInWall(prepared.Seat, wall.Boundary),
    UseCardCommand use => new CardPlayed(prepared.Seat, use.Cell, Session.Field.CardAt(use.Cell)!.OnPlayFromHand.Options[use.Option].Description),
    _ => null,
  };

  private (GameSession, CommandProcessor) Build()
  {
    var session = new GameSession(
      Setup.Seats.Select(seat => new Player(seat.Name, seat.Color, GameSession.StartingMoney) { IsBot = seat.IsBot, BotLevel = seat.BotLevel }),
      new SecureSeededRandom(Setup.RandomKey),
      Setup.Mode);
    session.Field.FailureConsequences = Setup.FailureConsequences;
    session.Start();
    FinishRounds(session);
    return (session, new CommandProcessor(session));
  }

  private void FinishRounds() => FinishRounds(Session, _recorder);

  /// <summary>
  /// Все спасовали — провести конец раунда (и следующий, если в новом раунде всем сразу нечем ходить).
  /// </summary>
  private static void FinishRounds(GameSession session, TableEventRecorder? recorder = null)
  {
    while (session.Phase == GamePhase.HeadquartersCheck)
      RoundEnd.Run(session, recorder is null ? null : recorder.AddIncome, recorder is null ? null : recorder.AddBonus);
  }
}
