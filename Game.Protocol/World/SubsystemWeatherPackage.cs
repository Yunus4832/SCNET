using MessagePack;

namespace Game.Protocol.World;

[MessagePackObject]
public sealed record SubsystemWeatherPackage : IProtocolMessage
{
    [Key(0)] public double FogEndTime { get; init; }

    [Key(1)] public float FogRampTime { get; init; }

    [Key(2)] public double FogStartTime { get; init; }

    [Key(3)] public float LightningIntensity { get; init; }

    [Key(4)] public double PrecipitationEndTime { get; init; }

    [Key(5)] public double PrecipitationStartTime { get; init; }
}
