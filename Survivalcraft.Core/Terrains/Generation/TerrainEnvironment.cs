namespace Game.Terrains.Generation;

/// <summary>Creates environment queries for every supported terrain generation mode.</summary>
public static class TerrainEnvironment
{
    public static ITerrainEnvironment Create(WorldSettings settings, int seed)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Generator.Definition.CreateEnvironment(settings, seed);
    }
}
