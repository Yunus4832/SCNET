namespace Game;

/// <summary>
///     地形生成模式
/// </summary>
public enum TerrainGenerationMode
{
    /// <summary>
    ///     大陆
    /// </summary>
    Continent = 0,

    /// <summary>
    ///     岛屿
    /// </summary>
    Island = 1,

    /// <summary>
    ///     平坦大陆
    /// </summary>
    FlatContinent = 2,

    /// <summary>
    ///     平坦岛屿
    /// </summary>
    FlatIsland = 3,

    LegacyContinentPre21 = 4,

    LegacyIslandPre21 = 5,

    LegacyFlatContinentPre21 = 6,

    LegacyFlatIslandPre21 = 7,

    LegacyContinent21 = 8,

    LegacyIsland21 = 9,

    LegacyFlatContinent21 = 10,

    LegacyFlatIsland21 = 11,

    LegacyContinent22 = 12,

    LegacyIsland22 = 13,

    LegacyFlatContinent22 = 14,

    LegacyFlatIsland22 = 15,

    LegacyContinent23 = 16,

    LegacyIsland23 = 17,

    LegacyFlatContinent23 = 18,

    LegacyFlatIsland23 = 19,
}

public static class TerrainGenerationModes
{
    public static bool IsLegacy(TerrainGenerationMode mode)
    {
        return mode >= TerrainGenerationMode.LegacyContinentPre21;
    }

    public static bool IsFlat(TerrainGenerationMode mode)
    {
        return mode is TerrainGenerationMode.FlatContinent or TerrainGenerationMode.FlatIsland
            or TerrainGenerationMode.LegacyFlatContinentPre21 or TerrainGenerationMode.LegacyFlatIslandPre21
            or TerrainGenerationMode.LegacyFlatContinent21 or TerrainGenerationMode.LegacyFlatIsland21
            or TerrainGenerationMode.LegacyFlatContinent22 or TerrainGenerationMode.LegacyFlatIsland22
            or TerrainGenerationMode.LegacyFlatContinent23 or TerrainGenerationMode.LegacyFlatIsland23;
    }

    public static bool IsIsland(TerrainGenerationMode mode)
    {
        return mode is TerrainGenerationMode.Island or TerrainGenerationMode.FlatIsland
            or TerrainGenerationMode.LegacyIslandPre21 or TerrainGenerationMode.LegacyFlatIslandPre21
            or TerrainGenerationMode.LegacyIsland21 or TerrainGenerationMode.LegacyFlatIsland21
            or TerrainGenerationMode.LegacyIsland22 or TerrainGenerationMode.LegacyFlatIsland22
            or TerrainGenerationMode.LegacyIsland23 or TerrainGenerationMode.LegacyFlatIsland23;
    }

    public static bool IsPre21(TerrainGenerationMode mode)
    {
        return mode is TerrainGenerationMode.LegacyContinentPre21 or TerrainGenerationMode.LegacyIslandPre21
            or TerrainGenerationMode.LegacyFlatContinentPre21 or TerrainGenerationMode.LegacyFlatIslandPre21;
    }

    public static TerrainGenerationMode ToDisplayMode(TerrainGenerationMode mode)
    {
        return mode switch
        {
            TerrainGenerationMode.LegacyIslandPre21 or TerrainGenerationMode.LegacyIsland21
                or TerrainGenerationMode.LegacyIsland22
                or TerrainGenerationMode.LegacyIsland23 => TerrainGenerationMode.Island,
            TerrainGenerationMode.LegacyFlatContinentPre21 or TerrainGenerationMode.LegacyFlatContinent21
                or TerrainGenerationMode.LegacyFlatContinent22
                or TerrainGenerationMode.LegacyFlatContinent23 => TerrainGenerationMode.FlatContinent,
            TerrainGenerationMode.LegacyFlatIslandPre21 or TerrainGenerationMode.LegacyFlatIsland21
                or TerrainGenerationMode.LegacyFlatIsland22
                or TerrainGenerationMode.LegacyFlatIsland23 => TerrainGenerationMode.FlatIsland,
            TerrainGenerationMode.Continent or TerrainGenerationMode.Island
                or TerrainGenerationMode.FlatContinent or TerrainGenerationMode.FlatIsland => mode,
            _ => TerrainGenerationMode.Continent
        };
    }

    public static TerrainGenerationMode ToNonFlatMode(TerrainGenerationMode mode)
    {
        return IsIsland(mode) ? TerrainGenerationMode.Island : TerrainGenerationMode.Continent;
    }
}
