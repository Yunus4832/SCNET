using Game.Protocol.Connection;

using MessagePack;

namespace Game.Protocol.Mods;

[MessagePackObject]
public sealed record ModEnvelopePackage() : IProtocolMessage
{
    [Key(0)] public string ModId { get; init; } = string.Empty;

    [Key(1)] public string MessageType { get; init; } = string.Empty;

    [Key(2)] public byte[] Payload { get; init; } = [];

    [Key(3)] public ClientState RequiredState { get; init; } = ClientState.Connected;

    public ModEnvelopePackage(string modId, string messageType, byte[] payload) : this()
    {
        ModId = modId;
        MessageType = messageType;
        Payload = payload;
    }
}
