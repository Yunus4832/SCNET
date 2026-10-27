using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record EntityHealthPackage : ComponentHealthPackage
{
    [IgnoreMember] public override EventType Type => EventType.SyncHealth;

    [Key(0)] public int TargetId { get; init; }

    [Key(1)] public float Health { get; init; }

    [Key(2)] public string Cause { get; init; } = string.Empty;
}
