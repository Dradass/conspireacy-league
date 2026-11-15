using System.Text.Json;
using System.Text.Json.Serialization;
using ConspiracyLeague.Core.Field;

namespace ConspiracyLeague.Core.Commands;

/// <summary>
/// Ячейка поля в JSON — только столбец и строка: <c>{"column":4,"row":45}</c>.
/// </summary>
/// <remarks>
/// Свои вычисляемые свойства (соседние ячейки) у ячейки сериализовать нельзя — они бесконечны.
/// Разбор строгий: оба числа обязательны, других полей нет.
/// </remarks>
public sealed class CellJsonConverter : JsonConverter<Cell>
{
  public override Cell Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    if (reader.TokenType != JsonTokenType.StartObject)
      throw new JsonException("Ячейка должна быть объектом.");

    int? column = null;
    int? row = null;
    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
    {
      if (reader.TokenType != JsonTokenType.PropertyName)
        throw new JsonException("Неверная ячейка.");
      var name = reader.GetString();
      if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var value))
        throw new JsonException("Координаты ячейки — целые числа.");
      switch (name)
      {
        case "column" when column is null:
          column = value;
          break;
        case "row" when row is null:
          row = value;
          break;
        default:
          throw new JsonException($"Лишнее поле ячейки: {name}.");
      }
    }

    if (column is null || row is null)
      throw new JsonException("У ячейки должны быть столбец и строка.");
    return new Cell(column.Value, row.Value);
  }

  public override void Write(Utf8JsonWriter writer, Cell value, JsonSerializerOptions options)
  {
    writer.WriteStartObject();
    writer.WriteNumber("column", value.Column);
    writer.WriteNumber("row", value.Row);
    writer.WriteEndObject();
  }
}
