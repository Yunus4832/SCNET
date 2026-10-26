namespace Game.Terrains;

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
