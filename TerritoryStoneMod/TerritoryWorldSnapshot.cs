using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace TerritoryStoneMod;

[MessagePackObject]
public sealed record TerritoryWorldSnapshot([property: Key(0), MaxLength(1024)] TerritorySnapshot[] Territories);
