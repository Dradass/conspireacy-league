using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConspiracyLeague.Core.Commands;

/// <summary>
/// Команды в JSON и обратно — для передачи по сети.
/// </summary>
/// <remarks>
/// Разбор строгий, потому что сообщение пришло от клиента, которому нельзя доверять: размер и вложенность
/// ограничены, лишние поля и неизвестные типы команд не принимаются. Никакой сериализации произвольных типов —
/// только перечисленные в <see cref="GameCommand"/> команды.
/// </remarks>
public static class CommandSerializer
{
  /// <summary>
  /// Больше команда в JSON быть не может, байт: самая длинная — розыгрыш карточки со всеми целями — намного короче.
  /// </summary>
  public const int MaxMessageBytes = 4096;

  private static readonly JsonSerializerOptions Options = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    MaxDepth = 8,
    NumberHandling = JsonNumberHandling.Strict,
    ReadCommentHandling = JsonCommentHandling.Disallow,
    AllowTrailingCommas = false,
    Converters = { new CellJsonConverter() },
  };

  /// <summary>
  /// Команда в JSON.
  /// </summary>
  public static string Serialize(GameCommand command) => JsonSerializer.Serialize(command, Options);

  /// <summary>
  /// Разобрать команду из JSON.
  /// </summary>
  /// <exception cref="CommandFormatException">Сообщение слишком длинное или это не команда.</exception>
  public static GameCommand Deserialize(string json)
  {
    ArgumentNullException.ThrowIfNull(json);
    // Каждый символ — хотя бы байт: длинную строку отбрасываем, не перекодируя её.
    if (json.Length > MaxMessageBytes || Encoding.UTF8.GetByteCount(json) > MaxMessageBytes)
      throw new CommandFormatException("Слишком длинная команда.");
    return Deserialize(Encoding.UTF8.GetBytes(json));
  }

  /// <summary>
  /// Разобрать команду из JSON в UTF-8.
  /// </summary>
  /// <exception cref="CommandFormatException">Сообщение слишком длинное или это не команда.</exception>
  public static GameCommand Deserialize(ReadOnlySpan<byte> utf8Json)
  {
    if (utf8Json.Length > MaxMessageBytes)
      throw new CommandFormatException("Слишком длинная команда.");

    try
    {
      return JsonSerializer.Deserialize<GameCommand>(utf8Json, Options)
        ?? throw new CommandFormatException("Пустая команда.");
    }
    catch (Exception error) when (error is JsonException or NotSupportedException or InvalidOperationException)
    {
      throw new CommandFormatException($"Неверная команда: {error.Message}");
    }
  }
}

/// <summary>
/// Сообщение не удалось разобрать как команду.
/// </summary>
public sealed class CommandFormatException(string message) : Exception(message);
