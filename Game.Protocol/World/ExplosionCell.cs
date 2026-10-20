using Engine.Core;

namespace Game.Protocol.World;

public readonly record struct ExplosionCell(Point3 Position, float Pressure);
