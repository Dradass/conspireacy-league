using ConspiracyLeague.Core.Protocol;

namespace ConspiracyLeague.Core.Server;

/// <summary>
/// Партия на сервере для подключённых игроков: принимает их сообщения (<see cref="Receive"/>), выполняет команды
/// и рассылает каждому его вид партии (<see cref="Outgoing"/>). Ничего не знает о сети — место игрока ему сообщает
/// транспорт, который знает, от какого соединения пришло сообщение; само сообщение место не указывает.
/// </summary>
/// <remarks>
/// <para>
/// Повторы: у каждой команды клиента есть номер. Команду с номером, который уже выполнялся, хост не выполняет
/// второй раз, а повторяет прежний ответ — так потерянный ответ безопасно запросить снова. Команды со старыми номерами
/// отбрасываются.
/// </para>
/// <para>
/// Порядок: сначала всем уходит обновление партии, потом отправителю — ответ на его команду. Получив ответ,
/// клиент уже видит её последствия.
/// </para>
/// <para>
/// Ходы ботов хост делает, когда его об этом просят (<see cref="RunBotStep"/>): так тот, кто его запускает,
/// сам решает, с какими паузами ходят боты.
/// </para>
/// </remarks>
public sealed class GameHost
{
  private readonly long[] _lastSeq;
  private readonly CommandReply?[] _lastReply;

  /// <param name="game">Партия.</param>
  public GameHost(ServerGame game)
  {
    ArgumentNullException.ThrowIfNull(game);
    Game = game;
    _lastSeq = new long[game.Session.Players.Count];
    _lastReply = new CommandReply?[game.Session.Players.Count];
    // События подготовки (стартовые руки, рынок) никому не нужны: подключившийся получает вид целиком.
    game.DrainEvents();
  }

  /// <summary>
  /// Партия.
  /// </summary>
  public ServerGame Game { get; }

  /// <summary>
  /// Отправить сообщение игроку; аргументы — место и сообщение в JSON. Игрокам-ботам сообщения не отправляются.
  /// </summary>
  public event Action<int, string>? Outgoing;

  /// <summary>
  /// Игрок подключился (или переподключился): отправить ему вид партии целиком.
  /// </summary>
  public void Connect(int seat)
  {
    RequireSeat(seat);
    SendView(seat);
  }

  /// <summary>
  /// Сообщение от игрока на месте <paramref name="seat"/>. Неверное сообщение не роняет хост: отправитель получает отказ.
  /// </summary>
  public void Receive(int seat, string json)
  {
    RequireSeat(seat);
    ClientMessage message;
    try
    {
      message = ProtocolSerializer.DeserializeClient(json);
    }
    catch (FormatException)
    {
      Send(seat, new CommandReply(0, false, "Сервер не смог разобрать сообщение."));
      return;
    }

    Receive(seat, message);
  }

  /// <summary>
  /// Уже разобранное сообщение от игрока. Сообщения не о партии (знакомство, лобби) здесь не обрабатываются.
  /// </summary>
  public void Receive(int seat, ClientMessage message)
  {
    RequireSeat(seat);
    switch (message)
    {
      case SyncRequest:
        SendView(seat);
        break;
      case CommandRequest request:
        Execute(seat, request);
        break;
    }
  }

  /// <summary>
  /// Сделать один ход бота, если сейчас очередь бота, и разослать обновление.
  /// </summary>
  /// <returns><c>true</c>, если бот действовал.</returns>
  public bool RunBotStep()
  {
    var before = Game.Version;
    var stepped = Game.RunBotStep();
    Broadcast(before);
    return stepped;
  }

  /// <summary>
  /// Сервер действует за игрока сам (например, игрок не успел походить): команда выполняется и рассылается обновление,
  /// но ответа никому нет — игрок эту команду не отправлял.
  /// </summary>
  public Commands.CommandResult ExecuteForPlayer(int seat, Commands.GameCommand command)
  {
    RequireSeat(seat);
    var before = Game.Version;
    var result = Game.Execute(seat, command);
    Broadcast(before);
    return result;
  }

  private void Execute(int seat, CommandRequest request)
  {
    if (request.Seq <= _lastSeq[seat])
    {
      // Повтор уже выполненной команды — ответ потерялся. Старые команды отбрасываем молча.
      if (request.Seq == _lastSeq[seat] && _lastReply[seat] is { } last)
        Send(seat, last);
      return;
    }

    var before = Game.Version;
    // Партия изменилась с тех пор, как игрок решил так походить: его решение относится к другой партии.
    var result = request.BasedOn is { } basedOn && basedOn != Game.Version
      ? Commands.CommandResult.Rejected("Партия изменилась, пока действие шло к серверу, — посмотрите на стол и походите ещё раз.")
      : Game.Execute(seat, request.Command);
    var reply = new CommandReply(request.Seq, result.Accepted, result.Error);
    _lastSeq[seat] = request.Seq;
    _lastReply[seat] = reply;
    Broadcast(before);
    Send(seat, reply);
  }

  private void Broadcast(long versionBefore)
  {
    var events = Game.DrainEvents();
    if (events.Count == 0 && Game.Version == versionBefore)
      return;

    for (var seat = 0; seat < _lastSeq.Length; seat++)
    {
      if (!Game.IsBotSeat(seat))
        Send(seat, new UpdateMessage(Game.UpdateFor(seat, events)));
    }
  }

  private void SendView(int seat) => Send(seat, new UpdateMessage(Game.UpdateFor(seat, [])));

  private void Send(int seat, ServerMessage message)
  {
    if (!Game.IsBotSeat(seat))
      Outgoing?.Invoke(seat, ProtocolSerializer.Serialize(message));
  }

  private void RequireSeat(int seat)
  {
    if (seat < 0 || seat >= _lastSeq.Length)
      throw new ArgumentOutOfRangeException(nameof(seat), "Такого места за столом нет.");
  }
}
