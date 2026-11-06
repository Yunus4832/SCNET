using System.Diagnostics.CodeAnalysis;

namespace Game.Servers;

public static class ServerInspectionProtocol
{
    public static bool TryDecodeResponse(
        ReadOnlyMemory<byte> payload,
        [NotNullWhen(true)] out ServerInfoResponsePackage? response
    )
    {
        response = null;
        try
        {
            var messages = ProtocolPacketCodec.Decode(payload);
            response = messages.Count == 1 ? messages[0] as ServerInfoResponsePackage : null;
            return response is { RequestInfo: false };
        }
        catch (Exception error) when (error is InvalidDataException or EndOfStreamException)
        {
            return false;
        }
    }
}
