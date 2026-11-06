using Game.Protocol.Identity;

using MessagePack;

namespace TerritoryStoneMod;

[MessagePackObject]
public sealed record TerritorySettingsResponse(
    [property: Key(0)] int RequestId,
    [property: Key(1)] PlayerAddress Owner,
    [property: Key(2)] bool Accepted,
    [property: Key(3)] string Error,
    [property: Key(4)] TerritorySettings Settings
);
