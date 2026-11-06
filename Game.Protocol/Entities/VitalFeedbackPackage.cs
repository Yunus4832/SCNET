using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record VitalFeedbackPackage : ComponentVitalStatPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.Feedback;

    [Key(1)] public int MessageKey { get; init; }

    [Key(2)] public bool IsEating { get; init; }
}
