using System.Text.Json.Nodes;

using MessagePack;
using MessagePack.Formatters;

namespace Game.Protocol.Serialization.Formatters;

internal sealed class JsonNodeFormatter : IMessagePackFormatter<JsonNode?>
{
    public static readonly JsonNodeFormatter Instance = new();

    public void Serialize(ref MessagePackWriter writer, JsonNode? value, MessagePackSerializerOptions options)
    {
        if (value is null) { writer.WriteNil(); }
        else { writer.Write(value.ToJsonString()); }
    }

    public JsonNode? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        return reader.TryReadNil() ? null : JsonNode.Parse(reader.ReadString()!);
    }
}
