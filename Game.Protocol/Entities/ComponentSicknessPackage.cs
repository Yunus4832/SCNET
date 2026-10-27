using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record ComponentSicknessPackage : IProtocolMessage
{
    [Key(0)] public int EntityId { get; init; }

    [Key(1)] public float SicknessDuration { get; init; }
}
