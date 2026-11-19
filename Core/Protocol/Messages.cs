using System.Text.Json.Serialization;
using ConspiracyLeague.Core.Bots;
using ConspiracyLeague.Core.Commands;
using ConspiracyLeague.Core.Game;
using ConspiracyLeague.Core.Views;

namespace ConspiracyLeague.Core.Protocol;

/// <summary>
/// Версия протокола. Клиент и сервер разных версий не играют вместе: сервер отказывает при знакомстве (<see cref="Hello"/>).
/// </summary>
public static class ProtocolInfo
{
  /// <summary>
  /// Текущая версия протокола. Меняется при любом несовместимом изменении сообщений или правил.
  /// </summary>
  public const int Version = 3;
}

/// <summary>
/// Сообщение клиента серверу.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(Hello), "hello")]
[JsonDerivedType(typeof(Pong), "pong")]
[JsonDerivedType(typeof(LobbyAddBot), "addBot")]
[JsonDerivedType(typeof(LobbyRemoveSeat), "removeSeat")]
[JsonDerivedType(typeof(LobbyStart), "start")]
[JsonDerivedType(typeof(Leave), "leave")]
[JsonDerivedType(typeof(CommandRequest), "command")]
[JsonDerivedType(typeof(SyncRequest), "sync")]
public abstract record ClientMessage;

/// <summary>
/// Знакомство — первое сообщение клиента. С токеном (<see cref="Welcome.Token"/>) клиент возвращается на своё место
/// после обрыва связи, без токена — садится за стол новым игроком.
/// </summary>
/// <param name="Protocol">Версия протокола клиента (<see cref="ProtocolInfo.Version"/>).</param>
/// <param name="Name">Имя игрока.</param>
/// <param name="Token">Токен места, выданный сервером раньше, или <c>null</c>.</param>
public sealed record Hello(int Protocol, string Name, string? Token = null) : ClientMessage;

/// <summary>
/// Ответ на проверку связи (<see cref="Ping"/>).
/// </summary>
public sealed record Pong(long Nonce) : ClientMessage;

/// <summary>
/// Создатель стола добавляет бота.
/// </summary>
public sealed record LobbyAddBot(BotLevel Level) : ClientMessage;

/// <summary>
/// Создатель стола убирает место (бота или игрока) до начала партии.
/// </summary>
public sealed record LobbyRemoveSeat(int Index) : ClientMessage;

/// <summary>
/// Создатель стола начинает партию.
/// </summary>
/// <param name="Mode">Режим.</param>
/// <param name="FailureConsequences">Последствия провала на низких гранях.</param>
/// <param name="TurnSeconds">Сколько секунд ждать действия игрока, от 0 до <see cref="MaxTurnSeconds"/>; 0 — без ограничения
/// (пока игрок сам не походит или не спасует). <c>null</c> — как настроено на сервере.</param>
public sealed record LobbyStart(GameMode Mode, bool FailureConsequences, int? TurnSeconds = null) : ClientMessage
{
  /// <summary>
  /// Дольше ждать хода нельзя.
  /// </summary>
  public const int MaxTurnSeconds = 3600;
}

/// <summary>
/// Игрок уходит из-за стола. До начала партии его место освобождается, во время партии — остаётся за ним
/// (он может вернуться по токену), а ходы за него по таймеру делает сервер.
/// </summary>
public sealed record Leave : ClientMessage;

/// <summary>
/// Ход игрока. Номер (<paramref name="Seq"/>) растёт с каждой новой командой: если ответ потерялся и клиент прислал
/// ту же команду ещё раз, сервер не выполнит её второй раз, а повторит ответ (<see cref="Server.GameHost"/>).
/// </summary>
/// <param name="Seq">Номер команды у этого клиента, с единицы.</param>
/// <param name="Command">Команда.</param>
/// <param name="BasedOn">Версия партии, глядя на которую игрок решил так походить (<see cref="PlayerView.Version"/>).
/// Если партия с тех пор изменилась (например, сервер походил за игрока, пока команда шла после обрыва связи),
/// команда отклоняется: решение принималось по другой партии.</param>
public sealed record CommandRequest(long Seq, GameCommand Command, long? BasedOn = null) : ClientMessage;

/// <summary>
/// Клиент просит прислать вид партии целиком (например, после переподключения или если не смог разобрать обновление).
/// </summary>
public sealed record SyncRequest : ClientMessage;

/// <summary>
/// Сообщение сервера клиенту.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(Welcome), "welcome")]
[JsonDerivedType(typeof(Ping), "ping")]
[JsonDerivedType(typeof(LobbyState), "lobby")]
[JsonDerivedType(typeof(GameStarted), "started")]
[JsonDerivedType(typeof(TurnTimer), "timer")]
[JsonDerivedType(typeof(Notice), "notice")]
[JsonDerivedType(typeof(Disconnecting), "bye")]
[JsonDerivedType(typeof(CommandReply), "reply")]
[JsonDerivedType(typeof(UpdateMessage), "update")]
public abstract record ServerMessage;

/// <summary>
/// Клиент принят за стол.
/// </summary>
/// <param name="Protocol">Версия протокола сервера.</param>
/// <param name="Token">Токен места: с ним клиент вернётся на своё место после обрыва связи. Секрет — его знает
/// только этот клиент; сервер хранит лишь его хеш.</param>
public sealed record Welcome(int Protocol, string Token) : ServerMessage;

/// <summary>
/// Проверка связи: клиент отвечает <see cref="Pong"/> с тем же числом. Клиента, который долго молчит, сервер отключает.
/// </summary>
public sealed record Ping(long Nonce) : ServerMessage;

/// <summary>
/// Место за столом.
/// </summary>
/// <param name="Name">Имя.</param>
/// <param name="IsBot">Бот.</param>
/// <param name="BotLevel">Уровень бота.</param>
/// <param name="Connected">Игрок на связи (у бота — всегда).</param>
/// <param name="IsHost">Создатель стола: он добавляет ботов и начинает партию.</param>
public sealed record LobbySeat(string Name, bool IsBot, BotLevel BotLevel, bool Connected, bool IsHost);

/// <summary>
/// Кто сидит за столом. Рассылается при каждом изменении: кто-то пришёл, ушёл, потерял связь или вернулся.
/// </summary>
/// <param name="Seats">Места по порядку; в партии номер места — его номер здесь.</param>
/// <param name="You">Место получателя.</param>
/// <param name="Started">Партия уже идёт.</param>
public sealed record LobbyState(IReadOnlyList<LobbySeat> Seats, int You, bool Started) : ServerMessage;

/// <summary>
/// Партия началась (или клиент вернулся в идущую партию). Следом придёт вид партии (<see cref="UpdateMessage"/>).
/// </summary>
/// <param name="Seat">Место получателя в партии.</param>
public sealed record GameStarted(int Seat) : ServerMessage;

/// <summary>
/// Ждём действия игрока: если он не успеет, за него походит сервер (пас, отказ от выбора).
/// </summary>
/// <param name="Seat">Чьё действие ждём.</param>
/// <param name="Seconds">Сколько секунд осталось; <c>null</c> — время хода не ограничено.</param>
public sealed record TurnTimer(int Seat, int? Seconds) : ServerMessage;

/// <summary>
/// Сообщение игроку: почему сервер не выполнил его просьбу или что произошло за столом.
/// </summary>
public sealed record Notice(string Text) : ServerMessage;

/// <summary>
/// Сервер закрывает соединение и объясняет почему. Клиент не переподключается сам, если <paramref name="Final"/>.
/// </summary>
/// <param name="Reason">Причина.</param>
/// <param name="Final">Возвращаться бесполезно (неверная версия, место занято, клиента выгнали).</param>
public sealed record Disconnecting(string Reason, bool Final) : ServerMessage;

/// <summary>
/// Ответ на команду клиента: принята она или нет и почему. Сами изменения партии приходят обновлением
/// (<see cref="UpdateMessage"/>) — оно отправляется раньше ответа.
/// </summary>
/// <param name="Seq">Номер команды (<see cref="CommandRequest.Seq"/>); 0 — сообщение клиента не удалось разобрать.</param>
/// <param name="Accepted">Команда принята.</param>
/// <param name="Error">Почему не принята.</param>
public sealed record CommandReply(long Seq, bool Accepted, string? Error) : ServerMessage;

/// <summary>
/// Партия изменилась: что произошло и как она теперь выглядит для этого игрока.
/// </summary>
public sealed record UpdateMessage(ServerUpdate Update) : ServerMessage;
