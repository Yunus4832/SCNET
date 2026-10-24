using Engine.Core.Mathematics;

namespace Game.Protocol.Characters;

public readonly record struct CharacterTerrainHit(
    Ray3 Ray,
    int Value,
    int CollisionBoxIndex,
    float Distance,
    Point3 Cell,
    int Face
);
