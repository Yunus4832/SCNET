namespace Game.Protocol.Serialization;

public sealed class ProtocolPacketMeasurements
{
    public int PayloadBytes { get; internal set; }

    public long SerializationTicks { get; internal set; }

    public long FramingTicks { get; internal set; }

    public List<(Type PackageType, int PayloadBytes)>? Messages { get; init; }
}
