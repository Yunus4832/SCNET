using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record WakeupPackage : ComponentSleepPackage
{
    [IgnoreMember] public override EventType Type => EventType.WakeUp;

    [Key(1)] public SleepWakeReason Reason { get; init; }
}
