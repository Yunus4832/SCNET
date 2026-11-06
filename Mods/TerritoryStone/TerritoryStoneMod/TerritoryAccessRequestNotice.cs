using Game.Protocol.Identity;

using MessagePack;

namespace TerritoryStoneMod;

[MessagePackObject]
public sealed record TerritoryAccessRequestNotice(
    [property: Key(0)] Guid Id,
    [property: Key(1)] PlayerAddress Owner,
    [property: Key(2)] PlayerAddress User,
    [property: Key(3)] string UserName
);
