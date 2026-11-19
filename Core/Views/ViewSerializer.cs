using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ConspiracyLeague.Core.Commands;

namespace ConspiracyLeague.Core.Views;

/// <summary>
/// Вид партии (<see cref="PlayerView"/>) в JSON и обратно — от сервера к клиенту.
/// </summary>
/// <remarks>
/// Клиент тоже разбирает сообщение строго: размер ограничен, лишних полей нет — так подменённый сервер или
/// испорченное сообщение не уронят игру.
/// </remarks>
public static class ViewSerializer
{
  /// <summary>
  /// Больше вид партии в JSON быть не может, байт: полная партия на 8 игроков в несколько раз меньше.
  /// </summary>
  public const int MaxMessageBytes = 1 << 20;

  private static readonly JsonSerializerOptions Options = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    MaxDepth = 16,
    NumberHandling = JsonNumberHandling.Strict,
    Converters = { new CellJsonConverter(), new JsonStringEnumConverter(allowIntegerValues: false) },
  };

  /// <summary>
  /// Вид партии в JSON.
  /// </summary>
  public static string Serialize(PlayerView view) => JsonSerializer.Serialize(view, Options);

  /// <summary>
  /// Сообщение сервера игроку (события и вид партии) в JSON.
  /// </summary>
  public static string Serialize(ServerUpdate update) => JsonSerializer.Serialize(update, Options);

  /// <summary>
  /// Разобрать сообщение сервера игроку из JSON.
  /// </summary>
  /// <exception cref="FormatException">Сообщение слишком длинное или это не сообщение сервера.</exception>
  public static ServerUpdate DeserializeUpdate(string json) => Parse<ServerUpdate>(json);

  /// <summary>
  /// Разобрать вид партии из JSON.
  /// </summary>
  /// <exception cref="FormatException">Сообщение слишком длинное или это не вид партии.</exception>
  public static PlayerView Deserialize(string json) => Parse<PlayerView>(json);

  private static T Parse<T>(string json)
    where T : class
  {
    ArgumentNullException.ThrowIfNull(json);
    if (json.Length > MaxMessageBytes || Encoding.UTF8.GetByteCount(json) > MaxMessageBytes)
      throw new FormatException("Слишком длинное сообщение о партии.");

    try
    {
      return JsonSerializer.Deserialize<T>(json, Options) ?? throw new FormatException("Пустое сообщение о партии.");
    }
    catch (Exception error) when (error is JsonException or NotSupportedException or InvalidOperationException)
    {
      throw new FormatException($"Неверное сообщение о партии: {error.Message}", error);
    }
  }
}
