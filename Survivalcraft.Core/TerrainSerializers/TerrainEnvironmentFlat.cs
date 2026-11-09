namespace Game.TerrainSerializers;

/// <summary>Read-only terrain environment calculations without world mutation services.</summary>
public class TerrainEnvironmentFlat : ITerrainEnvironment
{
    protected readonly Vector2? islandSize;

    protected readonly Vector2 oceanCorner;

    protected readonly Vector2 shoreRoughnessAmplitude;

    protected readonly Vector2 shoreRoughnessFrequency;

    protected readonly Vector2 shoreRoughnessOctaves;

    protected readonly float[] shoreRoughnessOffset = new float[4];

    protected readonly WorldSettings worldSettings;

    public TerrainEnvironmentFlat(WorldSettings worldSettings, int seed)
    {
        this.worldSettings = worldSettings;
        oceanCorner = TerrainGenerationModes.IsPre21(worldSettings.TerrainGenerationMode)
            ? oceanCorner = new Vector2(2001f, 2001f)
            : oceanCorner = new Vector2(-199f, -199f);
        islandSize = TerrainGenerationModes.IsIsland(worldSettings.TerrainGenerationMode)
            ? new Vector2?(worldSettings.IslandSize)
            : null;
        shoreRoughnessAmplitude.X = MathUtils.Pow(worldSettings.ShoreRoughness, 2f) *
                                    (islandSize.HasValue ? MathUtils.Min(4f * islandSize.Value.X, 400f) : 400f);
        shoreRoughnessAmplitude.Y = MathUtils.Pow(worldSettings.ShoreRoughness, 2f) *
                                    (islandSize.HasValue ? MathUtils.Min(4f * islandSize.Value.Y, 400f) : 400f);
        shoreRoughnessFrequency = MathUtils.Lerp(0.5f, 1f, worldSettings.ShoreRoughness) *
                                  new Vector2(1f) /
                                  shoreRoughnessAmplitude;
        shoreRoughnessOctaves.X =
            (int)MathUtils.Clamp(MathUtils.Log(1f / shoreRoughnessFrequency.X) / MathUtils.Log(2f) - 1f, 1f, 7f);
        shoreRoughnessOctaves.Y =
            (int)MathUtils.Clamp(MathUtils.Log(1f / shoreRoughnessFrequency.Y) / MathUtils.Log(2f) - 1f, 1f, 7f);
        var random = new Random(seed);
        shoreRoughnessOffset[0] = random.Float(-2000f, 2000f);
        shoreRoughnessOffset[1] = random.Float(-2000f, 2000f);
        shoreRoughnessOffset[2] = random.Float(-2000f, 2000f);
        shoreRoughnessOffset[3] = random.Float(-2000f, 2000f);
    }

    public int OceanLevel => worldSettings.TerrainLevel + worldSettings.SeaLevelOffset;

    public float CalculateOceanShoreDistance(float x, float z)
    {
        var x2 = 0f;
        var x3 = 0f;
        var y = 0f;
        var y2 = 0f;
        if (shoreRoughnessAmplitude.X > 0f)
        {
            x2 = shoreRoughnessAmplitude.X *
                 SimplexNoise.OctavedNoise(
                     z + shoreRoughnessOffset[0],
                     shoreRoughnessFrequency.X,
                     (int)shoreRoughnessOctaves.X,
                     2f,
                     0.6f
                 );
            x3 = shoreRoughnessAmplitude.X *
                 SimplexNoise.OctavedNoise(
                     z + shoreRoughnessOffset[1],
                     shoreRoughnessFrequency.X,
                     (int)shoreRoughnessOctaves.X,
                     2f,
                     0.6f
                 );
        }

        if (shoreRoughnessAmplitude.Y > 0f)
        {
            y = shoreRoughnessAmplitude.Y *
                SimplexNoise.OctavedNoise(
                    x + shoreRoughnessOffset[2],
                    shoreRoughnessFrequency.Y,
                    (int)shoreRoughnessOctaves.Y,
                    2f,
                    0.6f
                );
            y2 = shoreRoughnessAmplitude.Y *
                 SimplexNoise.OctavedNoise(
                     x + shoreRoughnessOffset[3],
                     shoreRoughnessFrequency.Y,
                     (int)shoreRoughnessOctaves.Y,
                     2f,
                     0.6f
                 );
        }

        var vector = oceanCorner + new Vector2(x2, y);
        var vector2 = oceanCorner +
                      (islandSize ?? new Vector2(3.40282347E+38f)) +
                      new Vector2(x3, y2);
        return MathUtils.Min(x - vector.X, vector2.X - x, z - vector.Y, vector2.Y - z);
    }

    public float CalculateMountainRangeFactor(float x, float z)
    {
        return 0f;
    }

    public float CalculateHeight(float x, float z)
    {
        return worldSettings.TerrainLevel;
    }

    public int CalculateTemperature(float x, float z)
    {
        return MathUtils.Clamp(12 + (int)worldSettings.TemperatureOffset, 0, 15);
    }

    public int CalculateHumidity(float x, float z)
    {
        return MathUtils.Clamp(12 + (int)worldSettings.HumidityOffset, 0, 15);
    }
}
