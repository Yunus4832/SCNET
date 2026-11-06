using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record ComponentSicknessPackage : IProtocolMessage
{
    [Key(0)] public int EntityId { get; init; }

    [Key(1)] public float SicknessDuration { get; init; }

    [Key(2)] public long EffectSequence { get; init; }

    [Key(3)] public float PukeDuration { get; init; }

    [Key(4)] public float NauseaDuration { get; init; }

    [Key(5)] public bool IsNausea { get; init; }
}
