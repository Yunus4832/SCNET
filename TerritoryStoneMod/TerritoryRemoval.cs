using Game.Protocol.Identity;

using MessagePack;

namespace TerritoryStoneMod;

[MessagePackObject]
public sealed record TerritoryRemoval([property: Key(0)] PlayerAddress Owner);
