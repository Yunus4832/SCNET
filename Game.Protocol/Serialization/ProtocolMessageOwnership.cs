using System.Text.Json;
using System.Text.Json.Nodes;

namespace Game.Protocol.Serialization;

/// <summary>Captures a message's mutable contract graph without serialization or runtime reflection.</summary>
public static partial class ProtocolMessageOwnership
{
    public static IProtocolMessage Capture(IProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ProtocolMessageValidation.Validate(message);
        var captured = CaptureCore(message);
        ProtocolMessageValidation.Validate(captured);
        return captured;
    }

    /// <summary>Conservative payload accounting for an already captured or decoded message, not managed heap measurement.</summary>
    public static long EstimateQueueBytes(IProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return checked(64L + EstimateCore(message));
    }

    private static long TextCharge(string value) => checked(24L + value.Length * 2L);

    private static long SequenceCharge<T>(IEnumerable<T> values, Func<T, long> charge)
    {
        var bytes = 32L;
        foreach (var value in values) { bytes = checked(bytes + 16L + charge(value)); }

        return bytes;
    }

    private static long JsonCharge(JsonNode? value)
    {
        return value switch
        {
            null => 0,
            JsonObject obj => SequenceCharge(obj, entry => checked(TextCharge(entry.Key) + JsonCharge(entry.Value))),
            JsonArray array => SequenceCharge(array, JsonCharge),
            JsonValue scalar when scalar.GetValueKind() == JsonValueKind.String =>
                TextCharge(scalar.GetValue<string>()),
            JsonValue scalar when scalar.GetValueKind() is JsonValueKind.Number or JsonValueKind.True
                or JsonValueKind.False
                or JsonValueKind.Null => 32,
            _ => throw new InvalidDataException("Queue charging requires a materialized JSON contract tree.")
        };
    }
}
