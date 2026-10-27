using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record EntityInjuryPackage : ComponentHealthPackage
{
    [IgnoreMember] public override EventType Type => EventType.Injure;

    [Key(0)] public int TargetId { get; init; }

    [Key(1)] public int AttackerId { get; init; }

    [Key(2)] public float Health { get; init; }

    [Key(3)] public bool IgnoreInvulnerability { get; init; }

    [Key(4)] public float Amount { get; init; }

    [Key(5)] public string Cause { get; init; } = string.Empty;
}
