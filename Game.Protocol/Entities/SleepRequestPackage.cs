using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record SleepRequestPackage : ComponentSleepPackage
{
    [IgnoreMember] public override EventType Type => EventType.SleepRequest;

    [Key(1)] public bool AllowManualWakeup { get; init; }
}
