using Engine.Core;

namespace Game.Protocol.Players;

public readonly record struct PlayerTerrainHit(
    Ray3 Ray,
    int Value,
    int CollisionBoxIndex,
    float Distance,
    Point3 Cell,
    int Face
);
