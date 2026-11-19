using ConspiracyLeague.Core.Commands;
using ConspiracyLeague.Core.Protocol;
using ConspiracyLeague.Core.Views;

namespace ConspiracyLeague.Core.Client;

/// <summary>
/// Клиент партии: отправляет на сервер команды игрока и собирает партию по присланным видам (<see cref="Mirror"/>).
/// Ничего не знает о сети: сообщения отправляет переданная ему функция, а принятые ему передают (<see cref="Receive"/>).
/// </summary>
/// <remarks>
/// <para>
/// Одна команда за раз: пока сервер не ответил на прошлую (<see cref="IsWaiting"/>), новая не отправляется —
/// так двойной щелчок не превратится в два хода.
/// </para>
/// <para>
/// Сервер может прислать что угодно, поэтому клиент проверяет и его: неверное сообщение или противоречивый вид
/// не роняют клиент и не портят партию — партия остаётся прежней, а клиент сообщает об ошибке (<see cref="ProtocolError"/>).
/// Обновления старше уже применённого отбрасываются.
/// </para>
/// </remarks>
public sealed class GameClient
{
  private readonly Action<string> _send;
  private long _nextSeq = 1;
  private string? _pendingJson;

  /// <param name="send">Отправить сообщение на сервер.</param>
  /// <param name="othersAsBots">Партия для интерфейса: чужие места помечены ботами (см. <see cref="ClientMirror"/>).</param>
  public GameClient(Action<string> send, bool othersAsBots = false)
  {
    ArgumentNullException.ThrowIfNull(send);
    _send = send;
    _othersAsBots = othersAsBots;
  }

  private readonly bool _othersAsBots;

  /// <summary>
  /// Партия, собранная по последнему виду от сервера, или <c>null</c>, пока вида не было.
  /// </summary>
  public ClientMirror? Mirror { get; private set; }

  /// <summary>
  /// Номер команды, на которую сервер ещё не ответил, или <c>null</c>.
  /// </summary>
  public long? PendingSeq { get; private set; }

  /// <summary>
  /// Ждём ответа сервера на команду.
  /// </summary>
  public bool IsWaiting => PendingSeq is not null;

  /// <summary>
  /// Применён новый вид партии; аргумент — обновление (события и вид).
  /// </summary>
  public event Action<ServerUpdate>? Updated;

  /// <summary>
  /// Сервер ответил на команду.
  /// </summary>
  public event Action<CommandReply>? Replied;

  /// <summary>
  /// Сообщение сервера не удалось разобрать или применить; аргумент — описание.
  /// </summary>
  public event Action<string>? ProtocolError;

  /// <summary>
  /// Отправить команду.
  /// </summary>
  /// <returns><c>false</c>, если ещё ждём ответа на прошлую команду или партии ещё нет.</returns>
  public bool Send(GameCommand command)
  {
    ArgumentNullException.ThrowIfNull(command);
    if (IsWaiting || Mirror is null)
      return false;

    var seq = _nextSeq++;
    _pendingJson = ProtocolSerializer.Serialize(new CommandRequest(seq, command, Mirror.View.Version));
    PendingSeq = seq;
    _send(_pendingJson);
    return true;
  }

  /// <summary>
  /// Отправить неотвеченную команду ещё раз (ответ мог потеряться). Сервер не выполнит её дважды.
  /// </summary>
  public void ResendPending()
  {
    if (_pendingJson is { } json)
      _send(json);
  }

  /// <summary>
  /// Попросить сервер прислать вид партии целиком.
  /// </summary>
  public void RequestSync() => _send(ProtocolSerializer.Serialize(new SyncRequest()));

  /// <summary>
  /// Сообщение от сервера.
  /// </summary>
  public void Receive(string json)
  {
    ServerMessage message;
    try
    {
      message = ProtocolSerializer.DeserializeServer(json);
    }
    catch (FormatException error)
    {
      ProtocolError?.Invoke(error.Message);
      return;
    }

    Receive(message);
  }

  /// <summary>
  /// Уже разобранное сообщение от сервера. Сообщения не о партии (лобби, проверка связи) пропускаются.
  /// </summary>
  public void Receive(ServerMessage message)
  {
    // Показ придержал партию: сообщения о ней ждут своей очереди, чтобы ответ не обогнал обновление.
    if (_holds > 0 && message is UpdateMessage or CommandReply)
    {
      _held.AddLast((message, false));
      return;
    }

    Process(message, announced: false);
  }

  /// <summary>
  /// Придержать обновления партии: пока показ не закончится (<see cref="Resume"/>), новые виды не применяются,
  /// а копятся по порядку. Так интерфейс успевает показать, например, бросок кубика на карточке до того,
  /// как она перевернётся.
  /// </summary>
  public void Pause() => _holds++;

  /// <summary>
  /// Показ закончился: применить придержанное (если других задержек нет).
  /// </summary>
  public void Resume()
  {
    if (_holds == 0)
      return;
    _holds--;
    while (_holds == 0 && _held.First is { } first)
    {
      _held.RemoveFirst();
      Process(first.Value.Message, first.Value.Announced);
    }
  }

  /// <summary>
  /// Обновлений придержано.
  /// </summary>
  public bool IsPaused => _holds > 0;

  private readonly LinkedList<(ServerMessage Message, bool Announced)> _held = new();
  private int _holds;

  /// <summary>
  /// Пришло новое обновление, но ещё не применено: <see cref="Mirror"/> пока прежняя. Обработчик может придержать его
  /// (<see cref="Pause"/>) — оно применится после <see cref="Resume"/>.
  /// </summary>
  public event Action<ServerUpdate>? Arriving;

  private void Process(ServerMessage message, bool announced)
  {
    switch (message)
    {
      case UpdateMessage { Update: var update }:
        if (!announced && Mirror is { } current && update.View.Version > current.View.Version)
        {
          Arriving?.Invoke(update);
          if (_holds > 0)
          {
            // Впереди очереди: остальные придержанные пришли позже.
            _held.AddFirst((message, true));
            return;
          }
        }

        Apply(update);
        break;
      case CommandReply reply:
        if (reply.Seq != 0 && reply.Seq == PendingSeq)
        {
          PendingSeq = null;
          _pendingJson = null;
        }
        Replied?.Invoke(reply);
        break;
    }
  }

  private void Apply(ServerUpdate update)
  {
    if (Mirror is { } current && update.View.Version < current.View.Version)
      return;

    try
    {
      if (Mirror is null)
        Mirror = new ClientMirror(update.View, _othersAsBots);
      else
        Mirror.Apply(update.View);
    }
    // Вид противоречив — партия осталась прежней (вид проверяется целиком до применения).
    catch (InvalidDataException error)
    {
      ProtocolError?.Invoke($"Не удалось применить вид партии: {error.Message}");
      return;
    }
    // Чего проверка не предусмотрела: партию могло изменить наполовину — просим у сервера вид целиком.
    catch (Exception error) when (error is not OutOfMemoryException)
    {
      ProtocolError?.Invoke($"Не удалось применить вид партии: {error.Message}");
      RequestSync();
      return;
    }

    Updated?.Invoke(update);
  }
}
