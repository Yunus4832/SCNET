using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record BlockFuseAddedPackage : ComponentOnFirePackage
{
    [IgnoreMember] public override EventType Type => EventType.FuseAdded;

    [Key(0)] public Point3 Point { get; init; }
}
