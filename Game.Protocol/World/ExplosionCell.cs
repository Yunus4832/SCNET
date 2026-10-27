using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.World;

[MessagePackObject]
public readonly record struct ExplosionCell([property: Key(0)] Point3 Position, [property: Key(1)] float Pressure);
