using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public sealed class ModEnvelopePackage : IPackage
{
    public string ModId { get; set; } = string.Empty;

    public string MessageType { get; set; } = string.Empty;

    public byte[] Payload { get; set; } = [];

    public ClientState RequiredState { get; set; } = ClientState.Connected;

    public byte ID => (byte)PackageType.ModPackage;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => RequiredState;

    public ModEnvelopePackage()
    {
    }

    public ModEnvelopePackage(string modId, string messageType, byte[] payload)
    {
        ModId = modId;
        MessageType = messageType;
        Payload = payload;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(ModId);
        writer.Write(MessageType);
        writer.WriteEnum(RequiredState);
        writer.WriteBuff(Payload);
    }

    public void ReadData(PackageStreamReader reader)
    {
        ModId = reader.ReadString();
        MessageType = reader.ReadString();
        RequiredState = reader.ReadEnum<ClientState>();
        Payload = reader.ReadBuff();
    }
}
