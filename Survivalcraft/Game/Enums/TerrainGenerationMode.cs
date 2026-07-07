namespace Game;

/// <summary>
/// 地形生成模式
/// </summary>
public enum TerrainGenerationMode
{
    /// <summary>
    /// 大陆
    /// </summary>
    Continent,

    /// <summary>
    /// 岛屿
    /// </summary>
    Island,

    /// <summary>
    /// 平坦大陆
    /// </summary>
    FlatContinent,

    /// <summary>
    /// 平坦岛屿
    /// </summary>
    FlatIsland,

    LegacyContinentPre21,

    LegacyIslandPre21,

    LegacyFlatContinentPre21,

    LegacyFlatIslandPre21,

    LegacyContinent21,

    LegacyIsland21,

    LegacyFlatContinent21,

    LegacyFlatIsland21,

    LegacyContinent22,

    LegacyIsland22,

    LegacyFlatContinent22,

    LegacyFlatIsland22,

    LegacyContinent23,

    LegacyIsland23,

    LegacyFlatContinent23,

    LegacyFlatIsland23,
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
                or TerrainGenerationMode.LegacyIsland22 or TerrainGenerationMode.LegacyIsland23 => TerrainGenerationMode.Island,
            TerrainGenerationMode.LegacyFlatContinentPre21 or TerrainGenerationMode.LegacyFlatContinent21
                or TerrainGenerationMode.LegacyFlatContinent22 or TerrainGenerationMode.LegacyFlatContinent23 => TerrainGenerationMode.FlatContinent,
            TerrainGenerationMode.LegacyFlatIslandPre21 or TerrainGenerationMode.LegacyFlatIsland21
                or TerrainGenerationMode.LegacyFlatIsland22 or TerrainGenerationMode.LegacyFlatIsland23 => TerrainGenerationMode.FlatIsland,
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
