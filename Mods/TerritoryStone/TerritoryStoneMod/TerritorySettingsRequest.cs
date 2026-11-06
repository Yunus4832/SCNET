using Game.Protocol.Identity;

using MessagePack;

namespace TerritoryStoneMod;

[MessagePackObject]
public sealed record TerritorySettingsRequest(
    [property: Key(0)] int RequestId,
    [property: Key(1)] PlayerAddress Owner,
    [property: Key(2)] TerritorySettings Settings
);
