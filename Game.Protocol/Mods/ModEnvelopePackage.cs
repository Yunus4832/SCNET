using Game.Protocol.Connection;

namespace Game.Protocol.Mods;

public sealed record ModEnvelopePackage : IProtocolMessage
{
    public string ModId { get; init; } = string.Empty;

    public string MessageType { get; init; } = string.Empty;

    public byte[] Payload { get; init; } = [];

    public ClientState RequiredState { get; init; } = ClientState.Connected;

    public byte ID => (byte)PackageType.ModEnvelope;

    public ModEnvelopePackage()
    {
    }

    public ModEnvelopePackage(string modId, string messageType, byte[] payload)
    {
        ModId = modId;
        MessageType = messageType;
        Payload = payload;
    }
}
