using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record BlockFuseRemovedPackage : ComponentOnFirePackage
{
    [IgnoreMember] public override EventType Type => EventType.FuseRemoved;

    [Key(0)] public Point3 Point { get; init; }
}
