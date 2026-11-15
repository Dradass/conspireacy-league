using ConspiracyLeague.Core.Client;
using ConspiracyLeague.Core.Commands;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Protocol;
using ConspiracyLeague.Core.Views;
using Godot;

namespace ConspiracyLeague.Game.Net;

/// <summary>
/// Сетевая партия: интерфейс — клиент партии (<see cref="GameClient"/>). Он рисует партию, собранную по виду
/// от сервера (<see cref="Session"/>), а действия игрока отправляет командами (<see cref="Send"/>) — сам он партию
/// не меняет. Раунды, ботов и таймер хода ведёт сервер.
/// </summary>
public partial class NetworkGame : Node
{
  private NetworkConnection _connection = null!;
  private Action<bool>? _onReply;
  private bool _wasConnected = true;

  /// <summary>
  /// Партия по подключению к столу. Партия на нём уже должна идти (<see cref="GameClient.Mirror"/> есть).
  /// </summary>
  public static NetworkGame Create(NetworkConnection connection)
  {
    if (connection.Table.Game.Mirror is null)
      throw new InvalidOperationException("Партия ещё не пришла с сервера.");

    var game = new NetworkGame { Name = "NetworkGame", _connection = connection };
    var table = connection.Table;
    table.Game.Replied += game.OnReplied;
    table.Game.Updated += update => game.Updated?.Invoke(update);
    table.Game.ProtocolError += error => game.Message?.Invoke($"Ошибка связи с сервером: {error}");
    table.Notice += text => game.Message?.Invoke(text);
    table.Disconnecting += (reason, _) => game.Message?.Invoke(reason);
    table.TurnTimer += (seat, seconds) => game.TurnTimerStarted?.Invoke(seat, seconds);
    connection.Link.Disconnected += _ => game.SetConnected(false);
    connection.Link.Connected += () => game.SetConnected(true);
    return game;
  }

  /// <summary>
  /// Клиент партии.
  /// </summary>
  public GameClient Client => _connection.Table.Game;

  /// <summary>
  /// Партия, по которой рисует интерфейс. Объект не меняется — меняется его состояние (<see cref="GameSession.Restored"/>).
  /// </summary>
  public GameSession Session => Client.Mirror!.Session;

  /// <summary>
  /// Игрок этого компьютера.
  /// </summary>
  public Player LocalPlayer => Client.Mirror!.Player!;

  /// <summary>
  /// Последний таймер хода (мог прийти ещё до начала показа партии).
  /// </summary>
  public (int Seat, int? Seconds)? LastTimer => _connection.Table.LastTimer;

  /// <summary>
  /// Есть ли связь с сервером.
  /// </summary>
  public bool HasConnection => _connection.Link.IsConnected;

  /// <summary>
  /// Сервер больше не примет этого игрока (ушёл, выгнали, другая версия).
  /// </summary>
  public bool IsClosed => _connection.Table.Closed;

  /// <summary>
  /// Сообщение игроку: сервер отклонил действие, связь дала сбой и т. п.
  /// </summary>
  public event Action<string>? Message;

  /// <summary>
  /// Партия изменилась; аргумент — обновление от сервера (события и вид). К этому моменту <see cref="Session"/> уже новая.
  /// </summary>
  public event Action<ServerUpdate>? Updated;

  /// <summary>
  /// Сервер ждёт действия игрока; аргументы — место и сколько секунд осталось.
  /// </summary>
  public event Action<int, int?>? TurnTimerStarted;

  /// <summary>
  /// Связь с сервером пропала или восстановилась.
  /// </summary>
  public event Action<bool>? ConnectionChanged;

  /// <summary>
  /// Отправить действие игрока на сервер.
  /// </summary>
  /// <param name="command">Команда.</param>
  /// <param name="onReply">Вызывается, когда сервер ответит: принята ли команда. Если команду отправить нельзя
  /// (сервер ещё не ответил на прошлую), вызывается сразу с <c>false</c>.</param>
  /// <returns><c>false</c>, если команду не отправили.</returns>
  public bool Send(GameCommand command, Action<bool>? onReply = null)
  {
    if (Client.IsWaiting)
    {
      Message?.Invoke("Подождите: сервер ещё не ответил на прошлое действие.");
      onReply?.Invoke(false);
      return false;
    }

    _onReply = onReply;
    if (Client.Send(command))
      return true;

    _onReply = null;
    onReply?.Invoke(false);
    return false;
  }

  private void OnReplied(CommandReply reply)
  {
    if (!reply.Accepted)
      Message?.Invoke(reply.Error ?? "Сервер отклонил действие.");
    var onReply = _onReply;
    _onReply = null;
    onReply?.Invoke(reply.Accepted);
  }

  private void SetConnected(bool connected)
  {
    if (connected == _wasConnected)
      return;
    _wasConnected = connected;
    Message?.Invoke(connected ? "Связь с сервером восстановлена." : "Связь с сервером потеряна. Переподключение…");
    ConnectionChanged?.Invoke(connected);
  }

  public override void _Process(double delta)
  {
    try
    {
      _connection.Poll();
    }
    catch (Exception error)
    {
      // Ошибка в показе обновления не должна ронять игру: партия на сервере от неё не пострадала.
      GD.PrintErr($"Ошибка сетевой игры: {error}");
      Message?.Invoke($"Ошибка: {error.Message}");
    }
  }

  public override void _ExitTree() => _connection.Dispose();
}
