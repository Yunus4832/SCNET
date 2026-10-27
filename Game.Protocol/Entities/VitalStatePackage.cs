using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record VitalStatePackage : ComponentVitalStatPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.SyncStat;

    [Key(1)] public float Food { get; init; }

    [Key(2)] public float Sleep { get; init; }

    [Key(3)] public float Stamina { get; init; }

    [Key(4)] public float Temperature { get; init; }

    [Key(5)] public float Wetness { get; init; }
}
