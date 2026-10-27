using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record SleepResultPackage : ComponentSleepPackage
{
    [IgnoreMember] public override EventType Type => EventType.Sleep;

    [Key(1)] public bool AllowManualWakeup { get; init; }

    [Key(2)] public bool Result { get; init; }

    [Key(3)] public string Reason { get; init; } = string.Empty;
}
