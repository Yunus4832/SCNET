namespace TerritoryStoneMod;

public sealed record TerritoryAccessRequest(Guid Id, Guid Owner, Guid Player, string PlayerName,
    double ExpiresAt);
