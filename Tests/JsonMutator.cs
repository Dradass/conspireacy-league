using System.Text.Json;
using System.Text.Json.Nodes;

namespace ConspiracyLeague.Core.Tests;

/// <summary>
/// Портит JSON случайно, но правдоподобно: меняет числа на граничные, строки — на пустые и чужие, удаляет и дублирует
/// элементы массивов, убирает поля, ставит null. Так проверяется, что разбор и применение сообщений выдерживают
/// подделанные данные.
/// </summary>
internal sealed class JsonMutator(Random random)
{
  private static readonly long[] Numbers = [-1, 0, 1, 2, 7, 99, int.MaxValue, int.MinValue, long.MaxValue];
  private static readonly string[] Strings = ["", "x", "Ленин — гриб", "Рептилоиды у власти", "Money", "Citizen", "Professional", "\u0000", new string('я', 300)];

  /// <summary>
  /// Испортить JSON <paramref name="mutations"/> раз.
  /// </summary>
  public string Mutate(string json, int mutations)
  {
    var root = JsonNode.Parse(json)!;
    for (var i = 0; i < mutations; i++)
      root = MutateOnce(root);
    return root.ToJsonString();
  }

  private JsonNode MutateOnce(JsonNode root)
  {
    var nodes = new List<JsonNode>();
    Collect(root, nodes);
    var target = nodes[random.Next(nodes.Count)];
    var parent = target.Parent;
    var replacement = Replacement(target);
    if (parent is null)
      return replacement ?? root;

    switch (parent)
    {
      case JsonObject obj:
        var name = obj.First(pair => ReferenceEquals(pair.Value, target)).Key;
        if (random.Next(5) == 0)
          obj.Remove(name);
        else
          obj[name] = replacement;
        break;
      case JsonArray array:
        var index = IndexOf(array, target);
        switch (random.Next(4))
        {
          case 0:
            array.RemoveAt(index);
            break;
          case 1:
            array.Add(JsonNode.Parse(target.ToJsonString()));
            break;
          default:
            array[index] = replacement;
            break;
        }
        break;
    }

    return root;
  }

  private JsonNode? Replacement(JsonNode target)
  {
    if (random.Next(8) == 0)
      return null;

    return target switch
    {
      JsonValue value when value.GetValueKind() == JsonValueKind.Number => JsonValue.Create(random.Next(3) == 0
        ? value.GetValue<long>() + random.Next(-3, 4)
        : Numbers[random.Next(Numbers.Length)]),
      JsonValue value when value.GetValueKind() == JsonValueKind.String => JsonValue.Create(Strings[random.Next(Strings.Length)]),
      JsonValue value when value.GetValueKind() is JsonValueKind.True or JsonValueKind.False => JsonValue.Create(!value.GetValue<bool>()),
      JsonArray => new JsonArray(),
      JsonObject obj => random.Next(2) == 0 ? new JsonObject() : JsonNode.Parse(obj.ToJsonString()),
      _ => JsonValue.Create(0),
    };
  }

  private static void Collect(JsonNode node, List<JsonNode> nodes)
  {
    nodes.Add(node);
    switch (node)
    {
      case JsonObject obj:
        foreach (var (_, child) in obj.ToList())
        {
          if (child is not null)
            Collect(child, nodes);
        }
        break;
      case JsonArray array:
        foreach (var child in array.ToList())
        {
          if (child is not null)
            Collect(child, nodes);
        }
        break;
    }
  }

  private static int IndexOf(JsonArray array, JsonNode node)
  {
    for (var i = 0; i < array.Count; i++)
    {
      if (ReferenceEquals(array[i], node))
        return i;
    }

    throw new InvalidOperationException("Узла нет в массиве.");
  }
}
