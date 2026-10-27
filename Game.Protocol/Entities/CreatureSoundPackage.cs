using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record CreatureSoundPackage : ComponentBehaviorPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.CreatureSound;

    [Key(1)] public bool SkipIfRecentlyPlayed { get; init; }

    [Key(2)] public byte SoundKind { get; init; }
}
