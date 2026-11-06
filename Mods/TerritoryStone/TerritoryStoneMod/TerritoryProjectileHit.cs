using MessagePack;

namespace TerritoryStoneMod;

[MessagePackObject]
public sealed record TerritoryProjectileHit(
    [property: Key(0)] int Value,
    [property: Key(1)] int X,
    [property: Key(2)] int Y,
    [property: Key(3)] int Z
);
