using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public readonly record struct PickableSnapshot(
    [property: Key(0)] ushort Id,
    [property: Key(1)] int Count,
    [property: Key(2)] int Value,
    [property: Key(3)] Vector3 Position,
    [property: Key(4)] Vector3 Velocity,
    [property: Key(5)] Matrix? StuckMatrix
);
