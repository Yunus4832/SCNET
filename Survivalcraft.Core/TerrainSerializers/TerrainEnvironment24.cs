namespace Game.TerrainSerializers;

/// <summary>Read-only terrain environment calculations without world mutation services.</summary>
public class TerrainEnvironment24 : ITerrainEnvironment
{
    protected readonly float tgMountainsDetailFreq;

    protected readonly int tgMountainsDetailOctaves;

    protected readonly float tgMountainsDetailPersistence;

    protected readonly float tgSurfaceMultiplier;

    protected readonly Vector2 humidityOffset;

    protected readonly Vector2? islandSize;

    protected readonly Vector2 mountainsOffset;

    protected readonly Vector2 oceanCorner;

    protected readonly Vector2 riversOffset;

    protected readonly int seed;

    protected readonly Vector2 temperatureOffset;

    protected readonly WorldSettings worldSettings;

    protected readonly float tgBiomeScaling;

    protected readonly bool tgCavesAndPockets;

    protected readonly float tgDensityBias;

    protected readonly bool tgExtras;

    protected readonly float tgHeightBias;

    protected readonly float tgHillsFrequency;

    protected readonly int tgHillsOctaves;

    protected readonly float tgHillsPercentage;

    protected readonly float tgHillsPersistence;

    protected readonly float tgHillsStrength;

    protected readonly float tgIslandsFrequency;

    protected readonly float tgMinTurbulence;

    protected readonly float tgMountainRangeFreq;

    protected readonly float tgMountainsPercentage;

    protected readonly float tgMountainsStrength;

    protected readonly float tgOceanSlope;

    protected readonly float tgOceanSlopeVariation;

    protected readonly float tgRiversStrength;

    protected readonly float tgShoreFluctuations;

    protected readonly float tgShoreFluctuationsScaling;

    protected readonly float tgTurbulenceFreq;

    protected readonly int tgTurbulenceOctaves;

    protected readonly float tgTurbulencePersistence;

    protected readonly float tgTurbulenceStrength;

    protected readonly float tgTurbulenceZero;

    public TerrainEnvironment24(WorldSettings worldSettings, int seed)
    {
        this.worldSettings = worldSettings;
        this.seed = seed;
        islandSize = TerrainGenerationModes.IsIsland(worldSettings.TerrainGenerationMode)
            ? new Vector2?(worldSettings.IslandSize)
            : null;
        var random = new Random(seed);
        var num = islandSize.HasValue ? MathUtils.Min(islandSize.Value.X, islandSize.Value.Y) : float.MaxValue;
        oceanCorner = new Vector2(-200f, -200f);
        temperatureOffset = new Vector2(random.Float(-3000f, 3000f), random.Float(-3000f, 3000f));
        humidityOffset = new Vector2(random.Float(-3000f, 3000f), random.Float(-3000f, 3000f));
        mountainsOffset = new Vector2(random.Float(-3000f, 3000f), random.Float(-3000f, 3000f));
        riversOffset = new Vector2(random.Float(-3000f, 3000f), random.Float(-3000f, 3000f));
        tgBiomeScaling = (TerrainGenerationModes.IsIsland(worldSettings.TerrainGenerationMode) ? 1f : 1.75f) *
                         worldSettings.BiomeSize;
        tgShoreFluctuations = MathUtils.Clamp(2f * num, 0f, 150f);
        tgShoreFluctuationsScaling = MathUtils.Clamp(0.04f * num, 0.5f, 3f);
        tgOceanSlope = 0.006f;
        tgOceanSlopeVariation = 0.004f;
        tgIslandsFrequency = 0.01f;
        tgDensityBias = 55f;
        tgHeightBias = 1f;
        tgRiversStrength = 1f;
        tgMountainsStrength = 250f;
        tgMountainRangeFreq = 0.0006f;
        tgMountainsPercentage = 0.13f;
        tgMountainsDetailFreq = 0.003f;
        tgMountainsDetailOctaves = 4;
        tgMountainsDetailPersistence = 0.53f;
        tgHillsPercentage = 0.32f;
        tgHillsStrength = 32f;
        tgHillsOctaves = 1;
        tgHillsFrequency = 0.014f;
        tgHillsPersistence = 0.5f;
        tgTurbulenceStrength = 55f;
        tgTurbulenceFreq = 0.03f;
        tgTurbulenceOctaves = 1;
        tgTurbulencePersistence = 0.5f;
        tgMinTurbulence = 0.04f;
        tgTurbulenceZero = 0.84f;
        tgSurfaceMultiplier = 2f;
        tgExtras = true;
        tgCavesAndPockets = true;
    }

    public int OceanLevel => 64 + worldSettings.SeaLevelOffset;

    public float CalculateOceanShoreDistance(float x, float z)
    {
        if (islandSize.HasValue)
        {
            var num = CalculateOceanShoreX(z);
            var num2 = CalculateOceanShoreZ(x);
            var num3 = CalculateOceanShoreX(z + 1000f) + islandSize.Value.X;
            var num4 = CalculateOceanShoreZ(x + 1000f) + islandSize.Value.Y;
            return MathUtils.Min(x - num, z - num2, num3 - x, num4 - z);
        }

        var num5 = CalculateOceanShoreX(z);
        var num6 = CalculateOceanShoreZ(x);
        return MathUtils.Min(x - num5, z - num6);
    }

    public float CalculateMountainRangeFactor(float x, float z)
    {
        return SimplexNoise.OctavedNoise(
            x + mountainsOffset.X,
            z + mountainsOffset.Y,
            tgMountainRangeFreq / tgBiomeScaling,
            3,
            1.91f,
            0.75f,
            true
        );
    }

    public float CalculateHeight(float x, float z)
    {
        var num = tgOceanSlope +
                  tgOceanSlopeVariation *
                  MathUtils.PowSign(
                      2f *
                      SimplexNoise.OctavedNoise(x + mountainsOffset.X, z + mountainsOffset.Y, 0.01f, 1, 2f, 0.5f) -
                      1f,
                      0.5f
                  );
        var num2 = CalculateOceanShoreDistance(x, z);
        var num3 = MathUtils.Saturate(2f - 0.05f * MathUtils.Abs(num2));
        var num4 = MathUtils.Saturate(MathUtils.Sin(tgIslandsFrequency * num2));
        var num5 = MathUtils.Saturate(MathUtils.Saturate((0f - num) * num2) - 0.85f * num4);
        var num6 = MathUtils.Saturate(MathUtils.Saturate(0.05f * (0f - num2 - 10f)) - num4);
        var v = CalculateMountainRangeFactor(x, z);
        var f = (1f - num3) * SimplexNoise.OctavedNoise(x, z, 0.001f / tgBiomeScaling, 2, 2f, 0.5f);
        var f2 = (1f - num3) * SimplexNoise.OctavedNoise(x, z, 0.0017f / tgBiomeScaling, 2, 4f, 0.7f);
        var num7 = (1f - num6) * (1f - num3) * Squish(v, 1f - tgHillsPercentage, 1f - tgMountainsPercentage);
        var num8 = (1f - num6) * Squish(v, 1f - tgMountainsPercentage, 1f);
        var num9 = 1f * SimplexNoise.OctavedNoise(x, z, tgHillsFrequency, tgHillsOctaves, 1.93f, tgHillsPersistence);
        var amplitudeStep =
            MathUtils.Lerp(0.75f * tgMountainsDetailPersistence, 1.33f * tgMountainsDetailPersistence, f);
        var num10 = 1.5f *
                    SimplexNoise.OctavedNoise(
                        x,
                        z,
                        tgMountainsDetailFreq,
                        tgMountainsDetailOctaves,
                        1.98f,
                        amplitudeStep
                    ) -
                    0.5f;
        var num11 = MathUtils.Lerp(
            80f,
            35f,
            MathUtils.Saturate(1f * num8 + 0.5f * num7 + MathUtils.Saturate(1f - num2 / 30f))
        );
        var x2 = MathUtils.Lerp(-2f, -4f, MathUtils.Saturate(num8 + 0.5f * num7));
        var num12 = MathUtils.Saturate(
            1.5f -
            num11 *
            MathUtils.Abs(
                2f *
                SimplexNoise.OctavedNoise(x + riversOffset.X, z + riversOffset.Y, 0.001f, 4, 2f, 0.5f) -
                1f
            )
        );
        var num13 = -50f * num5 + tgHeightBias;
        var num14 = MathUtils.Lerp(0f, 8f, f);
        var num15 = MathUtils.Lerp(0f, -6f, f2);
        var num16 = tgHillsStrength * num7 * num9;
        var num17 = tgMountainsStrength * num8 * num10;
        var f3 = tgRiversStrength * num12;
        var num18 = num13 + num14 + num15 + num17 + num16;
        var num19 = MathUtils.Min(MathUtils.Lerp(num18, x2, f3), num18);
        return MathUtils.Clamp(64f + num19, 10f, 251f);
    }

    public int CalculateTemperature(float x, float z)
    {
        return MathUtils.Clamp(
            (int)(MathUtils.Saturate(
                      3f *
                      SimplexNoise.OctavedNoise(
                          x + temperatureOffset.X,
                          z + temperatureOffset.Y,
                          0.0015f / tgBiomeScaling,
                          5,
                          2f,
                          0.6f
                      ) -
                      1.1f +
                      worldSettings.TemperatureOffset / 16f
                  ) *
                  16f),
            0,
            15
        );
    }

    public int CalculateHumidity(float x, float z)
    {
        return MathUtils.Clamp(
            (int)(MathUtils.Saturate(
                      3f *
                      SimplexNoise.OctavedNoise(
                          x + humidityOffset.X,
                          z + humidityOffset.Y,
                          0.0012f / tgBiomeScaling,
                          5,
                          2f,
                          0.6f
                      ) -
                      0.9f +
                      worldSettings.HumidityOffset / 16f
                  ) *
                  16f),
            0,
            15
        );
    }

    public float CalculateOceanShoreX(float z)
    {
        return oceanCorner.X +
               tgShoreFluctuations *
               SimplexNoise.OctavedNoise(z, 0f, 0.005f / tgShoreFluctuationsScaling, 4, 1.95f, 1f);
    }

    public float CalculateOceanShoreZ(float x)
    {
        return oceanCorner.Y +
               tgShoreFluctuations *
               SimplexNoise.OctavedNoise(0f, x, 0.005f / tgShoreFluctuationsScaling, 4, 1.95f, 1f);
    }

    public static float Squish(float v, float zero, float one)
    {
        return MathUtils.Saturate((v - zero) / (one - zero));
    }
}
