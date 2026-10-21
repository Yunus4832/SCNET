using System.Text.Json;

namespace Rendezvous.Protocol;

/// <summary>Separates versioned rendezvous control messages from application datagrams.</summary>
public static class SignalCodec
{
    public const byte CurrentVersion = 1;

    public const int MaxDatagramBytes = 1200;

    private static ReadOnlySpan<byte> Magic => "SCRV"u8;

    public static byte[] Encode(SignalMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var payload = JsonSerializer.SerializeToUtf8Bytes(message);
        if (payload.Length + 5 > MaxDatagramBytes)
        {
            throw new ArgumentException("Signaling datagram exceeds its size limit.", nameof(message));
        }

        var result = new byte[payload.Length + 5];
        Magic.CopyTo(result);
        result[4] = CurrentVersion;
        payload.CopyTo(result, 5);
        return result;
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out SignalMessage? message)
    {
        message = null;
        if (payload.Length is <= 5 or > MaxDatagramBytes ||
            !payload[..4].SequenceEqual(Magic) ||
            payload[4] != CurrentVersion)
        {
            return false;
        }

        try
        {
            message = JsonSerializer.Deserialize<SignalMessage>(payload[5..]);
            return message is { RequestId: var id } &&
                   id != Guid.Empty &&
                   !string.IsNullOrEmpty(message.Operation) &&
                   message.Operation.Length <= 32 &&
                   message.Ticket is not null &&
                   message.Ticket.Length <= 64 &&
                   message.Challenge is null or { Length: <= 64 } &&
                   message.Endpoint is null or { Length: <= 128 } &&
                   LocalCandidateEndpoints.TryParse(message.LocalEndpoints, null, out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
