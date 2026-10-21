namespace TerritoryStoneMod;

public sealed record TerritorySettings(
    int Size = 16,
    bool AllowEntry = false,
    bool AllowExit = true,
    bool AllowDigging = false,
    bool AllowPlacing = false,
    bool ShowBoundary = true
)
{
    public const int MinimumSize = 4;

    public const int MaximumSize = 32;

    public bool IsValid => Size is >= MinimumSize and <= MaximumSize;
}
