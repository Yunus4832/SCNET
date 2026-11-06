using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record WakeupRequestPackage : ComponentSleepPackage
{
    [IgnoreMember] public override EventType Type => EventType.WakeupRequest;

    [Key(1)] public double SleepStartTime { get; init; }
}
