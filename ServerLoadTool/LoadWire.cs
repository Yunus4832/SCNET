namespace ServerLoadTool;

public static class LoadWire
{
    public static byte[] Encode(IProtocolMessage package, byte? playerId = null)
    {
        if (package is ComponentPlayerPackage player)
        { package = player with { FromPlayerId = playerId ?? throw new ArgumentException("Player action requires the connection player ID.") }; }
        return ProtocolPacketCodec.Encode([package]);
    }

    public static IReadOnlyList<IProtocolMessage> Decode(ReadOnlyMemory<byte> data) => ProtocolPacketCodec.Decode(data);
}
