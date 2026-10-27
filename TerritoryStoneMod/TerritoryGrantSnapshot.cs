using Game.Protocol.Identity;

using MessagePack;

namespace TerritoryStoneMod;

[MessagePackObject]
public sealed record TerritoryGrantSnapshot(
    [property: Key(0)] PlayerAddress Subject,
    [property: Key(1)] string Name,
    [property: Key(2)] TerritoryPermission Permissions
);
