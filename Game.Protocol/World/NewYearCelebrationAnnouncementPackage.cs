using MessagePack;

namespace Game.Protocol.World;

[MessagePackObject]
public sealed record NewYearCelebrationAnnouncementPackage : IProtocolMessage
{
    [Key(0)] public Guid CharacterId { get; init; }
}
