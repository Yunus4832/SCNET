using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record MovingBlockStatePackage : MovingBlockPackage
{
    [IgnoreMember] public override EventType Type => EventType.Update;

    [Key(2)] public Vector3 Position { get; init; }

    [Key(3)] public Vector3 Velocity { get; init; }

    [Key(4)] public float Speed { get; init; }
}
