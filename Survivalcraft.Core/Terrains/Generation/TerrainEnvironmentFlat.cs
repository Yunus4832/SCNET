namespace Game.Terrains.Generation;

/// <summary>Read-only terrain environment calculations without world mutation services.</summary>
public class TerrainEnvironmentFlat : ITerrainEnvironment
{
    private readonly int _seaOffset;

    private readonly float _temperatureOffset;

    private readonly float _humidityOffset;

    protected readonly Vector2? islandSize;

    protected readonly Vector2 oceanCorner;

    protected readonly Vector2 shoreRoughnessAmplitude;

    protected readonly Vector2 shoreRoughnessFrequency;

    protected readonly Vector2 shoreRoughnessOctaves;

    protected readonly float[] shoreRoughnessOffset = new float[4];

    protected readonly int terrainHeight;

    public TerrainEnvironmentFlat(WorldSettings worldSettings, int seed)
    {
        var parameters = worldSettings.Generator;
        var isIsland = parameters.Get<string>("shape") == "island";
        terrainHeight = (int)parameters.Get<double>("terrainHeight");
        var roughness = (float)parameters.Get<double>("shoreRoughness");
        _seaOffset = (int)parameters.Get<double>("seaLevelOffset");
        _temperatureOffset = (float)parameters.Get<double>("temperatureOffset");
        _humidityOffset = (float)parameters.Get<double>("humidityOffset");
        oceanCorner = new Vector2(-199f, -199f);
        islandSize = isIsland
            ? new Vector2?(
                new Vector2((float)parameters.Get<double>("islandWidth"), (float)parameters.Get<double>("islandLength"))
            )
            : null;
        shoreRoughnessAmplitude.X = MathUtils.Pow(roughness, 2f) *
                                    (islandSize.HasValue ? MathUtils.Min(4f * islandSize.Value.X, 400f) : 400f);
        shoreRoughnessAmplitude.Y = MathUtils.Pow(roughness, 2f) *
                                    (islandSize.HasValue ? MathUtils.Min(4f * islandSize.Value.Y, 400f) : 400f);
        shoreRoughnessFrequency = MathUtils.Lerp(0.5f, 1f, roughness) *
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

    public int OceanLevel => terrainHeight + _seaOffset;

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
        return terrainHeight;
    }

    public int CalculateTemperature(float x, float z)
    {
        return MathUtils.Clamp(12 + (int)_temperatureOffset, 0, 15);
    }

    public int CalculateHumidity(float x, float z)
    {
        return MathUtils.Clamp(12 + (int)_humidityOffset, 0, 15);
    }
}
