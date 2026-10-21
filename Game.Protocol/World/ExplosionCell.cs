using Engine.Core.Mathematics;

namespace Game.Protocol.World;

public readonly record struct ExplosionCell(Point3 Position, float Pressure);
