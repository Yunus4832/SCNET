namespace Game.TerrainSerializers;

/// <summary>Creates environment queries for every supported terrain generation mode.</summary>
public static class TerrainEnvironment
{
    public static ITerrainEnvironment Create(WorldSettings settings, int seed)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var mode = settings.TerrainGenerationMode;
        if (TerrainGenerationModes.IsFlat(mode)) { return new TerrainEnvironmentFlat(settings, seed); }

        return mode switch
        {
            TerrainGenerationMode.LegacyContinent22 or TerrainGenerationMode.LegacyIsland22 =>
                new TerrainEnvironment22(settings, seed),
            TerrainGenerationMode.LegacyContinent23 or TerrainGenerationMode.LegacyIsland23 =>
                new TerrainEnvironment23(settings, seed),
            TerrainGenerationMode.LegacyContinentPre21 or TerrainGenerationMode.LegacyIslandPre21 or
                TerrainGenerationMode.LegacyContinent21 or TerrainGenerationMode.LegacyIsland21 =>
                new TerrainEnvironment21(settings, seed),
            _ => new TerrainEnvironment24(settings, seed)
        };
    }
}
