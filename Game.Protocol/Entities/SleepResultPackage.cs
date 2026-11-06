using Game.Protocol.Localization;

using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record SleepResultPackage : ComponentSleepPackage
{
    [IgnoreMember] public override EventType Type => EventType.Sleep;

    [Key(1)] public bool AllowManualWakeup { get; init; }

    [Key(2)] public bool Result { get; init; }

    [Key(3)] public LocalizedText Reason { get; init; } = LocalizedText.Empty;

    [Key(4)] public double StartTime { get; init; }
}
