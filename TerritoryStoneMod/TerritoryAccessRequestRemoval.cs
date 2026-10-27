using MessagePack;

namespace TerritoryStoneMod;

[MessagePackObject]
public sealed record TerritoryAccessRequestRemoval([property: Key(0)] Guid Id);
