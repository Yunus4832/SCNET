using MessagePack;

namespace TerritoryStoneMod;

[MessagePackObject]
public sealed record TerritorySettings(
    [property: Key(0)] int Size = 16,
    [property: Key(1)] bool AllowEntry = false,
    [property: Key(2)] bool AllowExit = true,
    [property: Key(3)] bool AllowDigging = false,
    [property: Key(4)] bool AllowPlacing = false,
    [property: Key(5)] bool ShowBoundary = true
)
{
    public const int MinimumSize = 4;

    public const int MaximumSize = 32;

    [IgnoreMember] public bool IsValid => Size is >= MinimumSize and <= MaximumSize;
}
