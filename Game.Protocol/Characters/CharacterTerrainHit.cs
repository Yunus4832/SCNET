using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public readonly record struct CharacterTerrainHit(
    [property: Key(0)] Ray3 Ray,
    [property: Key(1)] int Value,
    [property: Key(2)] int CollisionBoxIndex,
    [property: Key(3)] float Distance,
    [property: Key(4)] Point3 Cell,
    [property: Key(5)] int Face
);
