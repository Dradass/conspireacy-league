using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ConspiracyLeague.Core.Commands;

namespace ConspiracyLeague.Core.Protocol;

/// <summary>
/// Сообщения протокола в JSON и обратно.
/// </summary>
/// <remarks>
/// Обе стороны разбирают строго: размер и вложенность ограничены, лишних полей и неизвестных типов нет.
/// Сообщения клиента короткие — длинное отбрасывается, не разбираясь.
/// </remarks>
public static class ProtocolSerializer
{
  /// <summary>
  /// Больше сообщение клиента быть не может, байт: команда (<see cref="CommandSerializer.MaxMessageBytes"/>) и номер.
  /// </summary>
  public const int MaxClientMessageBytes = CommandSerializer.MaxMessageBytes + 64;

  /// <summary>
  /// Больше сообщение сервера быть не может, байт.
  /// </summary>
  public const int MaxServerMessageBytes = 1 << 20;

  private static readonly JsonSerializerOptions ClientOptions = Strict(maxDepth: 10);

  private static readonly JsonSerializerOptions ServerOptions = Strict(maxDepth: 18);

  private static JsonSerializerOptions Strict(int maxDepth) => new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    MaxDepth = maxDepth,
    NumberHandling = JsonNumberHandling.Strict,
    ReadCommentHandling = JsonCommentHandling.Disallow,
    AllowTrailingCommas = false,
    Converters = { new CellJsonConverter(), new JsonStringEnumConverter(allowIntegerValues: false) },
  };

  /// <summary>
  /// Сообщение клиента в JSON.
  /// </summary>
  public static string Serialize(ClientMessage message) => JsonSerializer.Serialize(message, ClientOptions);

  /// <summary>
  /// Сообщение сервера в JSON.
  /// </summary>
  public static string Serialize(ServerMessage message) => JsonSerializer.Serialize(message, ServerOptions);

  /// <summary>
  /// Разобрать сообщение клиента.
  /// </summary>
  /// <exception cref="FormatException">Сообщение слишком длинное или неверное.</exception>
  public static ClientMessage DeserializeClient(string json) => Parse<ClientMessage>(json, MaxClientMessageBytes, ClientOptions);

  /// <summary>
  /// Разобрать сообщение сервера.
  /// </summary>
  /// <exception cref="FormatException">Сообщение слишком длинное или неверное.</exception>
  public static ServerMessage DeserializeServer(string json) => Parse<ServerMessage>(json, MaxServerMessageBytes, ServerOptions);

  private static T Parse<T>(string json, int maxBytes, JsonSerializerOptions options)
    where T : class
  {
    ArgumentNullException.ThrowIfNull(json);
    // Каждый символ — хотя бы байт: длинную строку отбрасываем, не перекодируя её.
    if (json.Length > maxBytes || Encoding.UTF8.GetByteCount(json) > maxBytes)
      throw new FormatException("Слишком длинное сообщение.");

    T? message;
    try
    {
      message = JsonSerializer.Deserialize<T>(json, options);
    }
    catch (Exception error) when (error is JsonException or NotSupportedException or InvalidOperationException)
    {
      throw new FormatException($"Неверное сообщение: {error.Message}", error);
    }

    // Обязательные поля-объекты: JSON с null там, где ждут команду или обновление, — тоже неверное сообщение.
    return message switch
    {
      null => throw new FormatException("Пустое сообщение."),
      CommandRequest { Command: null } => throw new FormatException("В сообщении нет команды."),
      CommandRequest { Seq: <= 0 } => throw new FormatException("Неверный номер команды."),
      Hello { Name: null } => throw new FormatException("В знакомстве нет имени."),
      LobbyState lobby when lobby.Seats is null || lobby.Seats.Any(seat => seat is null || seat.Name is null)
        => throw new FormatException("Неверный список мест."),
      UpdateMessage { Update: null } or UpdateMessage { Update.View: null } or UpdateMessage { Update.Events: null }
        => throw new FormatException("В сообщении нет обновления."),
      _ => message,
    };
  }
}
