namespace TerritoryStoneMod;

[Flags]
public enum TerritoryPermission
{
    None = 0,

    Enter = 1,

    Exit = 2,

    Dig = 4,

    Place = 8,

    All = Enter | Exit | Dig | Place
}
