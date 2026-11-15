using ConspiracyLeague.Core.Server;

namespace ConspiracyLeague.Core.Client;

/// <summary>
/// Соединение клиентов с сервером в одном процессе — та же партия, что по сети, только без сети: сообщения
/// так же идут в JSON и так же проверяются. Нужно для игры на одном компьютере через сетевой путь и для тестов.
/// </summary>
/// <remarks>
/// Сообщения доставляются по очереди, а не вложенными вызовами: если обработчик обновления на клиенте сразу
/// отправит команду, сервер выполнит её после того, как закончит с текущей.
/// </remarks>
public sealed class LocalLink
{
  private readonly Queue<Action> _queue = new();
  private readonly Dictionary<int, GameClient> _clients = [];
  private bool _pumping;

  /// <param name="host">Сервер партии.</param>
  public LocalLink(GameHost host)
  {
    ArgumentNullException.ThrowIfNull(host);
    Host = host;
    host.Outgoing += (seat, json) => Post(() =>
    {
      if (_clients.TryGetValue(seat, out var client))
        client.Receive(json);
    });
  }

  /// <summary>
  /// Сервер партии.
  /// </summary>
  public GameHost Host { get; }

  /// <summary>
  /// Подключить клиента к месту за столом. Клиент сразу получает вид партии.
  /// </summary>
  /// <param name="seat">Место.</param>
  /// <param name="othersAsBots">Партия для интерфейса: чужие места помечены ботами (см. <see cref="ClientMirror"/>).</param>
  /// <exception cref="InvalidOperationException">К этому месту уже подключён клиент.</exception>
  public GameClient Connect(int seat, bool othersAsBots = false)
  {
    if (_clients.ContainsKey(seat))
      throw new InvalidOperationException($"К месту {seat} уже подключён клиент.");

    var client = new GameClient(json => Post(() => Host.Receive(seat, json)), othersAsBots);
    _clients.Add(seat, client);
    Post(() => Host.Connect(seat));
    return client;
  }

  /// <summary>
  /// Сделать один ход бота на сервере и доставить обновления.
  /// </summary>
  /// <returns><c>true</c>, если бот действовал.</returns>
  public bool RunBotStep()
  {
    var stepped = false;
    Post(() => stepped = Host.RunBotStep());
    return stepped;
  }

  private void Post(Action action)
  {
    _queue.Enqueue(action);
    if (_pumping)
      return;

    _pumping = true;
    try
    {
      while (_queue.TryDequeue(out var next))
        next();
    }
    finally
    {
      _pumping = false;
    }
  }
}
