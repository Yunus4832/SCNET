using Game.Protocol.Identity;

namespace TerritoryStoneMod;

public sealed record TerritoryAccessRequest(
    Guid Id,
    PlayerAddress Owner,
    PlayerAddress User,
    string UserName,
    double ExpiresAt
);
