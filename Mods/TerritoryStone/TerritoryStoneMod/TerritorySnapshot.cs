using Engine.Core.Mathematics;

using Game.Protocol.Identity;

using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace TerritoryStoneMod;

[MessagePackObject]
public sealed record TerritorySnapshot(
    [property: Key(0)] PlayerAddress Owner,
    [property: Key(1)] Point3 StonePoint,
    [property: Key(2)] TerritorySettings Settings,
    [property: Key(3), MaxLength(128)] TerritoryGrantSnapshot[] Grants
);
