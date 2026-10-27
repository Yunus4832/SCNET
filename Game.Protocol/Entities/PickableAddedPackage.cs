using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record PickableAddedPackage : PickablePackage
{
    [IgnoreMember] public override PickType Type => PickType.Create;

    [Key(1)] public ushort Id { get; init; }

    [Key(2)] public int Count { get; init; }

    [Key(3)] public int Value { get; init; }

    [Key(4)] public Vector3 Position { get; init; }

    [Key(5)] public Vector3 Velocity { get; init; }

    [Key(6)] public Matrix? StuckMatrix { get; init; }
}
