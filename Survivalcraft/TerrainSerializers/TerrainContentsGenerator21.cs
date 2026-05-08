namespace Game.TerrainSerializers;

public class TerrainContentsGenerator21 : ITerrainContentsGenerator
{
    private static readonly List<TerrainBrush> _coalBrushes = [];

    private static readonly List<TerrainBrush> _ironBrushes = [];

    private static readonly List<TerrainBrush> _copperBrushes = [];

    private static readonly List<TerrainBrush> _saltpeterBrushes = [];

    private static readonly List<TerrainBrush> _sulphurBrushes = [];

    private static readonly List<TerrainBrush> _diamondBrushes = [];

    private static readonly List<TerrainBrush> _germaniumBrushes = [];

    private static readonly List<TerrainBrush> _dirtPocketBrushes = [];

    private static readonly List<TerrainBrush> _gravelPocketBrushes = [];

    private static readonly List<TerrainBrush> _limestonePocketBrushes = [];

    private static readonly List<TerrainBrush> _sandPocketBrushes = [];

    private static readonly List<TerrainBrush> _basaltPocketBrushes = [];

    private static readonly List<TerrainBrush> _granitePocketBrushes = [];

    private static readonly List<TerrainBrush> _clayPocketBrushes = [];

    private static readonly List<TerrainBrush> _waterPocketBrushes = [];

    private static readonly List<TerrainBrush> _magmaPocketBrushes = [];

    private static readonly List<List<TerrainBrush>> _caveBrushesByType = [];

    private static float _tgSurfaceMultiplier;

    private readonly Vector2 _humidityOffset;

    private readonly Vector2? _islandSize;

    private readonly Vector2 _mountainsOffset;

    private readonly Vector2 _oceanCorner;

    private readonly Vector2 _riversOffset;

    private readonly int _seed;

    private readonly Vector2 _temperatureOffset;

    private readonly float _tgBiomeScaling;

    private readonly bool _tgCavesAndPockets;

    private readonly float _tgDensityBias;

    private readonly bool _tgExtras;

    private readonly float _tgHeightBias;

    private readonly float _tgHillsStrength;

    private readonly float _tgIslandsFrequency;

    private readonly float _tgMountainsPercentage;

    private readonly float _tgMountainsPeriod;

    private readonly float _tgMountainsStrength;

    private readonly bool _tgNewBiomeNoise;

    private readonly float _tgOceanSlope;

    private readonly float _tgOceanSlopeVariation;

    private readonly float _tgRiversStrength;

    private readonly float _tgShoreFluctuations;

    private readonly float _tgShoreFluctuationsScaling;

    private readonly float _tgTurbulencePower;

    private readonly float _tgTurbulenceStrength;

    private readonly float _tgTurbulenceTopOffset;

    public SubsystemBottomSuckerBlockBehavior SubsystemBottomSuckerBlockBehavior;

    public SubsystemTerrain SubsystemTerrain;

    public WorldSettings WorldSettings;


    static TerrainContentsGenerator21()
    {
        CreateBrushes();
    }

    public TerrainContentsGenerator21(SubsystemTerrain subsystemTerrain)
    {
        SubsystemTerrain = subsystemTerrain;
        SubsystemBottomSuckerBlockBehavior =
            subsystemTerrain.Project.FindSubsystem<SubsystemBottomSuckerBlockBehavior>(true)!;
        var subsystemGameInfo = subsystemTerrain.Project.FindSubsystem<SubsystemGameInfo>(true)!;
        WorldSettings = subsystemGameInfo.WorldSettings;
        _seed = subsystemGameInfo.WorldSeed;
        _islandSize = WorldSettings.TerrainGenerationMode == TerrainGenerationMode.Island
            ? new Vector2?(WorldSettings.IslandSize)
            : null;
        var oldRandom = new OldRandom(100 + _seed);
        var random = new Random(_seed);
        if (string.IsNullOrEmpty(subsystemGameInfo.WorldSettings.OriginalSerializationVersion))
        {
            _oceanCorner = new Vector2(oldRandom.UniformFloat(2000f, 4000f), oldRandom.UniformFloat(2000f, 4000f));
            _temperatureOffset = new Vector2(1000f, 1000f);
            _humidityOffset = new Vector2(0f, 0f);
            _mountainsOffset = new Vector2(0f, 0f);
            _riversOffset = new Vector2(0f, 0f);
            _tgNewBiomeNoise = false;
            _tgBiomeScaling = 1f;
            _tgShoreFluctuations = 100f;
            _tgShoreFluctuationsScaling = 1f;
            _tgOceanSlope = 0.015f;
            _tgOceanSlopeVariation = 0f;
            _tgIslandsFrequency = 0.017f;
            _tgDensityBias = 57f;
            _tgHeightBias = 1f;
            _tgRiversStrength = 0f;
            _tgMountainsStrength = 56f;
            _tgMountainsPeriod = 0.0014f;
            _tgMountainsPercentage = 0.15f;
            _tgHillsStrength = 13f;
            _tgTurbulenceStrength = 13f;
            _tgTurbulenceTopOffset = 3f;
            _tgTurbulencePower = 0.5f;
            _tgSurfaceMultiplier = 1f;
            _tgExtras = true;
            _tgCavesAndPockets = true;
        }
        else if (string.CompareOrdinal(subsystemGameInfo.WorldSettings.OriginalSerializationVersion, "2.1") < 0)
        {
            _oceanCorner = new Vector2(oldRandom.UniformFloat(2000f, 4000f), oldRandom.UniformFloat(2000f, 4000f));
            _temperatureOffset = new Vector2(1000f, 1000f);
            _humidityOffset = new Vector2(0f, 0f);
            _mountainsOffset = new Vector2(0f, 0f);
            _riversOffset = new Vector2(0f, 0f);
            _tgNewBiomeNoise = false;
            _tgBiomeScaling = 1f;
            _tgShoreFluctuations = 100f;
            _tgShoreFluctuationsScaling = 1f;
            _tgOceanSlope = 0.015f;
            _tgOceanSlopeVariation = 0f;
            _tgIslandsFrequency = 0.017f;
            _tgDensityBias = 57f;
            _tgHeightBias = 1f;
            _tgRiversStrength = 0f;
            _tgMountainsStrength = 50f;
            _tgMountainsPeriod = 0.0014f;
            _tgMountainsPercentage = 0.15f;
            _tgHillsStrength = 10f;
            _tgTurbulenceStrength = 24f;
            _tgTurbulenceTopOffset = 0f;
            _tgTurbulencePower = 0.3f;
            _tgSurfaceMultiplier = 1f;
            _tgExtras = true;
            _tgCavesAndPockets = true;
        }
        else
        {
            var num = _islandSize.HasValue
                ? MathUtils.Min(_islandSize.Value.X, _islandSize.Value.Y)
                : 3.40282347E+38f;
            _oceanCorner = new Vector2(random.UniformFloat(-100f, -100f), random.UniformFloat(-100f, -100f));
            _temperatureOffset = new Vector2(random.UniformFloat(-2000f, 2000f), random.UniformFloat(-2000f, 2000f));
            _humidityOffset = new Vector2(random.UniformFloat(-2000f, 2000f), random.UniformFloat(-2000f, 2000f));
            _mountainsOffset = new Vector2(random.UniformFloat(-2000f, 2000f), random.UniformFloat(-2000f, 2000f));
            _riversOffset = new Vector2(random.UniformFloat(-2000f, 2000f), random.UniformFloat(-2000f, 2000f));
            _tgNewBiomeNoise = true;
            _tgBiomeScaling = 1.5f * WorldSettings.BiomeSize;
            _tgShoreFluctuations = MathUtils.Clamp(2f * num, 0f, 150f);
            _tgShoreFluctuationsScaling = MathUtils.Clamp(0.04f * num, 0.5f, 3f);
            _tgOceanSlope = 0.006f;
            _tgOceanSlopeVariation = 0.004f;
            _tgIslandsFrequency = 0.01f;
            _tgDensityBias = 55f;
            _tgHeightBias = 1f;
            _tgRiversStrength = 1f;
            _tgMountainsStrength = 85f;
            _tgMountainsPeriod = 0.0015f;
            _tgMountainsPercentage = 0.15f;
            _tgHillsStrength = 8f;
            _tgTurbulenceStrength = 35f;
            _tgTurbulenceTopOffset = 0f;
            _tgTurbulencePower = 0.3f;
            _tgSurfaceMultiplier = 2f;
            _tgExtras = true;
            _tgCavesAndPockets = true;
        }
    }

    public int OceanLevel => 64 + WorldSettings.SeaLevelOffset;

    public Vector3 FindCoarseSpawnPosition()
    {
        var vector = Vector2.Zero;
        var num = -3.40282347E+38f;
        for (var i = 0; i < 800; i += 2)
        for (var j = 4; j <= 8; j += 2)
        for (var k = 0; k < 2; k++)
        {
            float num2;
            float x;
            if (k == 0)
            {
                num2 = _oceanCorner.Y + i;
                x = CalculateOceanShoreX(num2) + j;
            }
            else
            {
                x = _oceanCorner.X + i;
                num2 = CalculateOceanShoreZ(x) + j;
            }

            var num3 = ScoreSpawnPosition(Terrain.ToCell(x), Terrain.ToCell(num2));
            if (num3 > num)
            {
                vector = new Vector2(x, num2);
                num = num3;
            }
        }

        return new Vector3(vector.X, CalculateHeight(vector.X, vector.Y), vector.Y);
    }

    public void GenerateChunkContentsPass1(TerrainChunk chunk)
    {
        GenerateSurfaceParameters(chunk, 0, 0, 16, 8);
        GenerateTerrain(chunk, 0, 0, 16, 8);
    }

    public void GenerateChunkContentsPass2(TerrainChunk chunk)
    {
        GenerateSurfaceParameters(chunk, 0, 8, 16, 16);
        GenerateTerrain(chunk, 0, 8, 16, 16);
    }

    public void GenerateChunkContentsPass3(TerrainChunk chunk)
    {
        GenerateCaves(chunk);
        GeneratePockets(chunk);
        GenerateMinerals(chunk);
        GenerateSurface(chunk);
        PropagateFluidsDownwards(chunk);
    }

    public void GenerateChunkContentsPass4(TerrainChunk chunk)
    {
        GenerateGrassAndPlants(chunk);
        GenerateTreesAndLogs(chunk);
        GenerateCacti(chunk);
        GeneratePumpkins(chunk);
        GenerateKelp(chunk);
        GenerateSeagrass(chunk);
        GenerateBottomSuckers(chunk);
        GenerateTraps(chunk);
        GenerateIvy(chunk);
        GenerateGraves(chunk);
        GenerateSnowAndIce(chunk);
        GenerateBedrockAndAir(chunk);
        UpdateFluidIsTop(chunk);
    }

    public float CalculateOceanShoreDistance(float x, float z)
    {
        if (_islandSize.HasValue)
        {
            var num = CalculateOceanShoreX(z);
            var num2 = CalculateOceanShoreZ(x);
            var num3 = CalculateOceanShoreX(z + 1000f) + _islandSize.Value.X;
            var num4 = CalculateOceanShoreZ(x + 1000f) + _islandSize.Value.Y;
            return MathUtils.Min(x - num, z - num2, num3 - x, num4 - z);
        }

        var num5 = CalculateOceanShoreX(z);
        var num6 = CalculateOceanShoreZ(x);
        return MathUtils.Min(x - num5, z - num6);
    }

    public float CalculateMountainRangeFactor(float x, float z)
    {
        return 1f - MathUtils.Abs(2f * SimplexNoise.OctavedNoise(x + _mountainsOffset.X, z + _mountainsOffset.Y,
            _tgMountainsPeriod / _tgBiomeScaling, 3, 1.91f, 0.75f) - 1f);
    }

    public float CalculateHeight(float x, float z)
    {
        var num = _tgOceanSlope + _tgOceanSlopeVariation * MathUtils.PowSign(
            2f * SimplexNoise.OctavedNoise(x + _mountainsOffset.X, z + _mountainsOffset.Y, 0.01f, 1, 2f, 0.5f) - 1f,
            0.5f);
        var num2 = CalculateOceanShoreDistance(x, z);
        var num3 = MathUtils.Saturate(1f - 0.05f * MathUtils.Abs(num2));
        var num4 = MathUtils.Saturate(MathUtils.Sin(_tgIslandsFrequency * num2));
        var num5 = MathUtils.Saturate(MathUtils.Saturate((0f - num) * num2) - 0.85f * num4);
        var num6 = MathUtils.Saturate(MathUtils.Saturate(0.05f * (0f - num2 - 10f)) - num4);
        var num7 = CalculateMountainRangeFactor(x, z);
        var f = (1f - num3) * SimplexNoise.OctavedNoise(x, z, 0.001f / _tgBiomeScaling, 2, 1.97f, 0.8f);
        var f2 = (1f - num3) * SimplexNoise.OctavedNoise(x, z, 0.0017f / _tgBiomeScaling, 2, 1.93f, 0.7f);
        var num8 = (1f - num6) * (1f - num3) * MathUtils.Saturate((num7 - 0.6f) / 0.4f);
        var num9 = (1f - num6) * MathUtils.Saturate((num7 - (1f - _tgMountainsPercentage)) / _tgMountainsPercentage);
        var num10 = 2f * SimplexNoise.OctavedNoise(x, z, 0.02f, 3, 1.93f, 0.8f) - 1f;
        var num11 = 1.5f * SimplexNoise.OctavedNoise(x, z, 0.004f, 4, 1.98f, 0.9f) - 0.5f;
        var num12 = MathUtils.Lerp(60f, 30f,
            MathUtils.Saturate(1f * num9 + 0.5f * num8 + MathUtils.Saturate(1f - num2 / 30f)));
        var x2 = MathUtils.Lerp(-2f, -4f, MathUtils.Saturate(num9 + 0.5f * num8));
        var num13 = MathUtils.Saturate(1.5f - num12 *
            MathUtils.Abs(2f *
                SimplexNoise.OctavedNoise(x + _riversOffset.X, z + _riversOffset.Y, 0.001f, 4, 2f, 0.5f) - 1f));
        var num14 = -50f * num5 + _tgHeightBias;
        var num15 = MathUtils.Lerp(0f, 8f, f);
        var num16 = MathUtils.Lerp(0f, -6f, f2);
        var num17 = _tgHillsStrength * num8 * num10;
        var num18 = _tgMountainsStrength * num9 * num11;
        var f3 = _tgRiversStrength * num13;
        var num19 = num14 + num15 + num16 + num18 + num17;
        var num20 = MathUtils.Min(MathUtils.Lerp(num19, x2, f3), num19);
        return MathUtils.Clamp(64f + num20, 10f, 251f);
    }

    public int CalculateTemperature(float x, float z)
    {
        if (_tgNewBiomeNoise)
        {
            return MathUtils.Clamp(
                (int)(MathUtils.Saturate(4f * SimplexNoise.OctavedNoise(x + _temperatureOffset.X,
                                             z + _temperatureOffset.Y, 0.0015f / _tgBiomeScaling, 5, 2f, 0.7f) - 1.6f +
                                         WorldSettings.TemperatureOffset / 16f) * 16f), 0, 15);
        }

        return MathUtils.Clamp(
            (int)((MathUtils.Saturate(4f * SimplexNoise.OctavedNoise(x + _temperatureOffset.X,
                       z + _temperatureOffset.Y, 0.0006f / _tgBiomeScaling, 4, 1.93f, 1f) - 1.6f) +
                   WorldSettings.TemperatureOffset / 16f) * 16f), 0, 15);
    }

    public int CalculateHumidity(float x, float z)
    {
        if (_tgNewBiomeNoise)
        {
            return MathUtils.Clamp(
                (int)(MathUtils.Saturate(
                    4f * SimplexNoise.OctavedNoise(x + _humidityOffset.X, z + _humidityOffset.Y,
                        0.0012f / _tgBiomeScaling, 5, 2f, 0.7f) - 1.2f + WorldSettings.HumidityOffset / 16f) * 16f), 0,
                15);
        }

        return MathUtils.Clamp(
            (int)((MathUtils.Saturate(4f * SimplexNoise.OctavedNoise(x + _humidityOffset.X, z + _humidityOffset.Y,
                0.0008f / _tgBiomeScaling, 5, 1.97f, 1f) - 1.5f) + WorldSettings.HumidityOffset / 16f) * 16f), 0, 15);
    }

    public float CalculateOceanShoreX(float z)
    {
        return _oceanCorner.X + _tgShoreFluctuations *
            SimplexNoise.OctavedNoise(z, 0f, 0.005f / _tgShoreFluctuationsScaling, 4, 1.95f, 1f);
    }

    public float CalculateOceanShoreZ(float x)
    {
        return _oceanCorner.Y + _tgShoreFluctuations *
            SimplexNoise.OctavedNoise(0f, x, 0.005f / _tgShoreFluctuationsScaling, 4, 1.95f, 1f);
    }

    public float ScoreSpawnPosition(int x, int z)
    {
        var num = 0f;
        var num2 = CalculateOceanShoreDistance(x, z);
        var num3 = CalculateMountainRangeFactor(x, z);
        var num4 = CalculateHumidity(x, z);
        var num5 = CalculateTemperature(x, z);
        if (num2 < 0f)
        {
            num -= 1f;
        }

        if (num2 > 10f)
        {
            num -= 1f;
        }

        if (num3 > 0.66f)
        {
            num -= 0.5f;
        }

        if (num4 < 10)
        {
            num -= 1f;
        }

        if (num5 < 2)
        {
            num -= 0.5f;
        }

        var x2 = CalculateHeight(x, z);
        var x3 = CalculateHeight(x - 5, z - 5);
        var x4 = CalculateHeight(x - 5, z + 5);
        var x5 = CalculateHeight(x + 5, z - 5);
        var x6 = CalculateHeight(x + 5, z + 5);
        var num6 = MathUtils.Min(x2, MathUtils.Min(x3, x4, x5, x6));
        var num7 = MathUtils.Max(x2, MathUtils.Max(x3, x4, x5, x6));
        if (num6 < 64f || num7 > 75f)
        {
            num -= 1f;
        }

        return num;
    }

    public void GenerateSurfaceParameters(TerrainChunk chunk, int x1, int z1, int x2, int z2)
    {
        for (var i = x1; i < x2; i++)
        for (var j = z1; j < z2; j++)
        {
            var num = i + chunk.Origin.X;
            var num2 = j + chunk.Origin.Y;
            var temperature = CalculateTemperature(num, num2);
            var humidity = CalculateHumidity(num, num2);
            chunk.SetTemperatureFast(i, j, temperature);
            chunk.SetHumidityFast(i, j, humidity);
        }
    }

    public void GenerateTerrain(TerrainChunk chunk, int x1, int z1, int x2, int z2)
    {
        var num = x2 - x1;
        var num2 = z2 - z1;
        _ = SubsystemTerrain.Terrain;
        var num3 = chunk.Origin.X + x1;
        var num4 = chunk.Origin.Y + z1;
        var grid2D = new Grid2D(num, num2);
        var grid2D2 = new Grid2D(num, num2);
        for (var i = 0; i < num2; i++)
        for (var j = 0; j < num; j++)
        {
            grid2D.Set(j, i, CalculateOceanShoreDistance(j + num3, i + num4));
            grid2D2.Set(j, i, CalculateMountainRangeFactor(j + num3, i + num4));
        }

        var grid3D = new Grid3D(num / 4 + 1, 33, num2 / 4 + 1);
        for (var k = 0; k < grid3D.SizeX; k++)
        for (var l = 0; l < grid3D.SizeZ; l++)
        {
            var num5 = k * 4 + num3;
            var num6 = l * 4 + num4;
            var num7 = CalculateHeight(num5, num6);
            var num8 = CalculateMountainRangeFactor(num5, num6);
            var num9 = MathUtils.Saturate(0.9f * (num8 - 0.8f) / 0.2f + 0.1f);
            for (var m = 0; m < grid3D.SizeY; m++)
            {
                var num10 = m * 8;
                var num11 = num7 - _tgTurbulenceTopOffset;
                var num12 =
                    MathUtils.Lerp(0f, _tgTurbulenceStrength * num9, MathUtils.Saturate((num11 - num10) * 0.2f)) *
                    MathUtils.PowSign(
                        2f * SimplexNoise.OctavedNoise(num5, num10 + 1000, num6, 0.008f, 3, 2f, 0.75f) - 1f,
                        _tgTurbulencePower);
                var num13 = num10 + num12;
                var num14 = num7 - num13;
                num14 += MathUtils.Max(4f * (_tgDensityBias - num10), 0f);
                grid3D.Set(k, m, l, num14);
            }
        }

        var oceanLevel = OceanLevel;
        for (var n = 0; n < grid3D.SizeX - 1; n++)
        for (var num15 = 0; num15 < grid3D.SizeZ - 1; num15++)
        for (var num16 = 0; num16 < grid3D.SizeY - 1; num16++)
        {
            grid3D.Get8(n, num16, num15, out var v, out var v2, out var v3, out var v4, out var v5, out var v6,
                out var v7, out var v8);
            var num17 = (v2 - v) / 4f;
            var num18 = (v4 - v3) / 4f;
            var num19 = (v6 - v5) / 4f;
            var num20 = (v8 - v7) / 4f;
            var num21 = v;
            var num22 = v3;
            var num23 = v5;
            var num24 = v7;
            for (var num25 = 0; num25 < 4; num25++)
            {
                var num26 = (num23 - num21) / 4f;
                var num27 = (num24 - num22) / 4f;
                var num28 = num21;
                var num29 = num22;
                for (var num30 = 0; num30 < 4; num30++)
                {
                    var num31 = (num29 - num28) / 8f;
                    var num32 = num28;
                    var num33 = num25 + n * 4;
                    var num34 = num30 + num15 * 4;
                    var x3 = x1 + num33;
                    var z3 = z1 + num34;
                    var x4 = grid2D.Get(num33, num34);
                    var num35 = grid2D2.Get(num33, num34);
                    var temperatureFast = chunk.GetTemperatureFast(x3, z3);
                    var humidityFast = chunk.GetHumidityFast(x3, z3);
                    var f = num35 - 0.01f * humidityFast;
                    var num36 = MathUtils.Lerp(100f, 0f, f);
                    var num37 = MathUtils.Lerp(300f, 30f, f);
                    var flag = (temperatureFast > 8 && humidityFast < 8 && num35 < 0.95f) ||
                               (MathUtils.Abs(x4) < 12f && num35 < 0.9f);
                    var num38 = TerrainChunk.CalculateCellIndex(x3, 0, z3);
                    for (var num39 = 0; num39 < 8; num39++)
                    {
                        var num40 = num39 + num16 * 8;
                        var value = 0;
                        if (num32 < 0f)
                        {
                            if (num40 <= oceanLevel)
                            {
                                value = 18;
                            }
                        }
                        else
                        {
                            value = !flag ? !(num32 < num37) ? 67 : 3 :
                                !(num32 < num36) ? !(num32 < num37) ? 67 : 3 : 4;
                        }

                        chunk.SetCellValueFast(num38 + num40, value);
                        num32 += num31;
                    }

                    num28 += num26;
                    num29 += num27;
                }

                num21 += num17;
                num22 += num18;
                num23 += num19;
                num24 += num20;
            }
        }
    }

    public void GenerateSurface(TerrainChunk chunk)
    {
        var terrain = SubsystemTerrain.Terrain;
        var random = new Random(_seed + chunk.Coords.X + 101 * chunk.Coords.Y);
        for (var i = 0; i < 16; i++)
        for (var j = 0; j < 16; j++)
        {
            var num = i + chunk.Origin.X;
            var num2 = j + chunk.Origin.Y;
            var num3 = TerrainChunk.CalculateCellIndex(i, 254, j);
            var num4 = 254;
            while (num4 >= 0)
            {
                var num5 = Terrain.ExtractContents(chunk.GetCellValueFast(num3));
                if (!BlocksManager.Blocks[num5].Transparent)
                {
                    var num6 = CalculateMountainRangeFactor(num, num2);
                    var temperature = terrain.GetTemperature(num, num2);
                    var humidity = terrain.GetHumidity(num, num2);
                    var f = MathUtils.Saturate(MathUtils.Saturate((num6 - 0.9f) / 0.1f) -
                                               MathUtils.Saturate((humidity - 3f) / 12f) +
                                               _tgSurfaceMultiplier * MathUtils.Saturate((num4 - 85f) * 0.05f));
                    var min = (int)MathUtils.Lerp(4f, 0f, f);
                    var max = (int)MathUtils.Lerp(7f, 0f, f);
                    var num7 = MathUtils.Min(random.UniformInt(min, max), num4);
                    int num8;
                    if (num5 == 4)
                    {
                        num8 = temperature > 4 && temperature < 7 ? 6 : 7;
                    }
                    else
                    {
                        var num9 = temperature / 4;
                        var num10 = num4 + 1 < 255 ? chunk.GetCellContentsFast(i, num4 + 1, j) : 0;
                        num8 = (num4 < 66 || num4 == 84 + num9 || num4 == 103 + num9) && humidity == 9 &&
                               temperature % 6 == 1 ? 66 :
                            num10 != 18 || humidity <= 8 || humidity % 2 != 0 || temperature % 3 != 0 ? 2 : 72;
                    }

                    var num11 = TerrainChunk.CalculateCellIndex(i, num4 + 1, j);
                    for (var k = num11 - num7; k < num11; k++)
                    {
                        if (Terrain.ExtractContents(chunk.GetCellValueFast(k)) != 0)
                        {
                            var value = Terrain.ReplaceContents(0, num8);
                            chunk.SetCellValueFast(k, value);
                        }
                    }

                    break;
                }

                num4--;
                num3--;
            }
        }
    }

    public void GenerateMinerals(TerrainChunk chunk)
    {
        if (!_tgCavesAndPockets)
        {
            return;
        }

        var x = chunk.Coords.X;
        var y = chunk.Coords.Y;
        for (var i = x - 1; i <= x + 1; i++)
        for (var j = y - 1; j <= y + 1; j++)
        {
            var random = new Random(_seed + i + 119 * j);
            var num = random.UniformInt(0, 10);
            for (var k = 0; k < num; k++)
            {
                random.UniformInt(0, 1);
            }

            var num2 = CalculateMountainRangeFactor(i * 16, j * 16);
            var num3 = (int)(5f + 2f * num2 * SimplexNoise.OctavedNoise(i, j, 0.33f, 1, 1f, 1f));
            for (var l = 0; l < num3; l++)
            {
                var x2 = i * 16 + random.UniformInt(0, 15);
                var y2 = random.UniformInt(5, 80);
                var z = j * 16 + random.UniformInt(0, 15);
                _coalBrushes[random.UniformInt(0, _coalBrushes.Count - 1)].PaintFastSelective(chunk, x2, y2, z, 3);
            }

            var num4 = (int)(6f + 2f * num2 * SimplexNoise.OctavedNoise(i + 1211, j + 396, 0.33f, 1, 1f, 1f));
            for (var m = 0; m < num4; m++)
            {
                var x3 = i * 16 + random.UniformInt(0, 15);
                var y3 = random.UniformInt(20, 65);
                var z2 = j * 16 + random.UniformInt(0, 15);
                _copperBrushes[random.UniformInt(0, _copperBrushes.Count - 1)]
                    .PaintFastSelective(chunk, x3, y3, z2, 3);
            }

            var num5 = (int)(5f + 2f * num2 * SimplexNoise.OctavedNoise(i + 713, j + 211, 0.33f, 1, 1f, 1f));
            for (var n = 0; n < num5; n++)
            {
                var x4 = i * 16 + random.UniformInt(0, 15);
                var y4 = random.UniformInt(2, 40);
                var z3 = j * 16 + random.UniformInt(0, 15);
                _ironBrushes[random.UniformInt(0, _ironBrushes.Count - 1)].PaintFastSelective(chunk, x4, y4, z3, 67);
            }

            var num6 = (int)(3f + 2f * num2 * SimplexNoise.OctavedNoise(i + 915, j + 272, 0.33f, 1, 1f, 1f));
            for (var num7 = 0; num7 < num6; num7++)
            {
                var x5 = i * 16 + random.UniformInt(0, 15);
                var y5 = random.UniformInt(50, 70);
                var z4 = j * 16 + random.UniformInt(0, 15);
                _saltpeterBrushes[random.UniformInt(0, _saltpeterBrushes.Count - 1)]
                    .PaintFastSelective(chunk, x5, y5, z4, 4);
            }

            var num8 = (int)(3f + 2f * num2 * SimplexNoise.OctavedNoise(i + 711, j + 1194, 0.33f, 1, 1f, 1f));
            for (var num9 = 0; num9 < num8; num9++)
            {
                var x6 = i * 16 + random.UniformInt(0, 15);
                var y6 = random.UniformInt(2, 40);
                var z5 = j * 16 + random.UniformInt(0, 15);
                _sulphurBrushes[random.UniformInt(0, _sulphurBrushes.Count - 1)]
                    .PaintFastSelective(chunk, x6, y6, z5, 67);
            }

            var num10 = (int)(0.5f + 2f * num2 * SimplexNoise.OctavedNoise(i + 432, j + 907, 0.33f, 1, 1f, 1f));
            for (var num11 = 0; num11 < num10; num11++)
            {
                var x7 = i * 16 + random.UniformInt(0, 15);
                var y7 = random.UniformInt(2, 15);
                var z6 = j * 16 + random.UniformInt(0, 15);
                _diamondBrushes[random.UniformInt(0, _diamondBrushes.Count - 1)]
                    .PaintFastSelective(chunk, x7, y7, z6, 67);
            }

            var num12 = (int)(3f + 2f * num2 * SimplexNoise.OctavedNoise(i + 799, j + 131, 0.33f, 1, 1f, 1f));
            for (var num13 = 0; num13 < num12; num13++)
            {
                var x8 = i * 16 + random.UniformInt(0, 15);
                var y8 = random.UniformInt(2, 50);
                var z7 = j * 16 + random.UniformInt(0, 15);
                _germaniumBrushes[random.UniformInt(0, _germaniumBrushes.Count - 1)]
                    .PaintFastSelective(chunk, x8, y8, z7, 67);
            }
        }
    }

    public void GeneratePockets(TerrainChunk chunk)
    {
        if (!_tgCavesAndPockets)
        {
            return;
        }

        for (var i = -1; i <= 1; i++)
        for (var j = -1; j <= 1; j++)
        {
            var num = i + chunk.Coords.X;
            var num2 = j + chunk.Coords.Y;
            var random = new Random(_seed + num + 71 * num2);
            var num3 = random.UniformInt(0, 10);
            for (var k = 0; k < num3; k++)
            {
                random.UniformInt(0, 1);
            }

            var num4 = CalculateMountainRangeFactor(num * 16, num2 * 16);
            for (var l = 0; l < 3; l++)
            {
                var x = num * 16 + random.UniformInt(0, 15);
                var y = random.UniformInt(50, 100);
                var z = num2 * 16 + random.UniformInt(0, 15);
                _dirtPocketBrushes[random.UniformInt(0, _dirtPocketBrushes.Count - 1)]
                    .PaintFastSelective(chunk, x, y, z, 3);
            }

            for (var m = 0; m < 10; m++)
            {
                var x2 = num * 16 + random.UniformInt(0, 15);
                var y2 = random.UniformInt(20, 80);
                var z2 = num2 * 16 + random.UniformInt(0, 15);
                _gravelPocketBrushes[random.UniformInt(0, _gravelPocketBrushes.Count - 1)]
                    .PaintFastSelective(chunk, x2, y2, z2, 3);
            }

            for (var n = 0; n < 2; n++)
            {
                var x3 = num * 16 + random.UniformInt(0, 15);
                var y3 = random.UniformInt(20, 120);
                var z3 = num2 * 16 + random.UniformInt(0, 15);
                _limestonePocketBrushes[random.UniformInt(0, _limestonePocketBrushes.Count - 1)]
                    .PaintFastSelective(chunk, x3, y3, z3, 3);
            }

            for (var num5 = 0; num5 < 1; num5++)
            {
                var x4 = num * 16 + random.UniformInt(0, 15);
                var y4 = random.UniformInt(50, 70);
                var z4 = num2 * 16 + random.UniformInt(0, 15);
                _clayPocketBrushes[random.UniformInt(0, _clayPocketBrushes.Count - 1)]
                    .PaintFastSelective(chunk, x4, y4, z4, 3);
            }

            for (var num6 = 0; num6 < 6; num6++)
            {
                var x5 = num * 16 + random.UniformInt(0, 15);
                var y5 = random.UniformInt(40, 80);
                var z5 = num2 * 16 + random.UniformInt(0, 15);
                _sandPocketBrushes[random.UniformInt(0, _sandPocketBrushes.Count - 1)]
                    .PaintFastSelective(chunk, x5, y5, z5, 4);
            }

            for (var num7 = 0; num7 < 4; num7++)
            {
                var x6 = num * 16 + random.UniformInt(0, 15);
                var y6 = random.UniformInt(40, 60);
                var z6 = num2 * 16 + random.UniformInt(0, 15);
                _basaltPocketBrushes[random.UniformInt(0, _basaltPocketBrushes.Count - 1)]
                    .PaintFastSelective(chunk, x6, y6, z6, 4);
            }

            for (var num8 = 0; num8 < 3; num8++)
            {
                var x7 = num * 16 + random.UniformInt(0, 15);
                var y7 = random.UniformInt(20, 40);
                var z7 = num2 * 16 + random.UniformInt(0, 15);
                _basaltPocketBrushes[random.UniformInt(0, _basaltPocketBrushes.Count - 1)]
                    .PaintFastSelective(chunk, x7, y7, z7, 3);
            }

            for (var num9 = 0; num9 < 6; num9++)
            {
                var x8 = num * 16 + random.UniformInt(0, 15);
                var y8 = random.UniformInt(4, 50);
                var z8 = num2 * 16 + random.UniformInt(0, 15);
                _granitePocketBrushes[random.UniformInt(0, _granitePocketBrushes.Count - 1)]
                    .PaintFastSelective(chunk, x8, y8, z8, 67);
            }

            if (random.Bool(0.02f + 0.01f * num4))
            {
                var num10 = num * 16;
                var num11 = random.UniformInt(40, 60);
                var num12 = num2 * 16;
                var num13 = random.UniformInt(1, 3);
                for (var num14 = 0; num14 < num13; num14++)
                {
                    var vector = random.Vector2(7f);
                    var num15 = 8 + (int)MathUtils.Round(vector.X);
                    var num16 = 0;
                    var num17 = 8 + (int)MathUtils.Round(vector.Y);
                    _waterPocketBrushes[random.UniformInt(0, _waterPocketBrushes.Count - 1)]
                        .PaintFast(chunk, num10 + num15, num11 + num16, num12 + num17);
                }
            }

            if (random.Bool(0.06f + 0.05f * num4))
            {
                var num18 = num * 16;
                var num19 = random.UniformInt(15, 42);
                var num20 = num2 * 16;
                var num21 = random.UniformInt(1, 2);
                for (var num22 = 0; num22 < num21; num22++)
                {
                    var vector2 = random.Vector2(7f);
                    var num23 = 8 + (int)MathUtils.Round(vector2.X);
                    var num24 = random.UniformInt(0, 1);
                    var num25 = 8 + (int)MathUtils.Round(vector2.Y);
                    _magmaPocketBrushes[random.UniformInt(0, _magmaPocketBrushes.Count - 1)]
                        .PaintFast(chunk, num18 + num23, num19 + num24, num20 + num25);
                }
            }
        }
    }

    public void GenerateCaves(TerrainChunk chunk)
    {
        if (!_tgCavesAndPockets)
        {
            return;
        }

        var list = new List<CavePoint>();
        var x = chunk.Coords.X;
        var y = chunk.Coords.Y;
        for (var i = x - 2; i <= x + 2; i++)
        for (var j = y - 2; j <= y + 2; j++)
        {
            list.Clear();
            var random = new Random(_seed + i + 9973 * j);
            var num = i * 16 + random.UniformInt(0, 15);
            var num2 = j * 16 + random.UniformInt(0, 15);
            var probability = 0.5f;
            if (!random.Bool(probability))
            {
                continue;
            }

            var num3 = (int)CalculateHeight(num, num2);
            var num4 = (int)CalculateHeight(num + 3, num2);
            var num5 = (int)CalculateHeight(num, num2 + 3);
            var position = new Vector3(num, num3 - 1, num2);
            var v = new Vector3(3f, num4 - num3, 0f);
            var v2 = new Vector3(0f, num5 - num3, 3f);
            var direction = Vector3.Normalize(Vector3.Cross(v, v2));
            if (direction.Y > -0.6f)
            {
                list.Add(new CavePoint
                {
                    Position = position,
                    Direction = direction,
                    BrushType = 0,
                    Length = random.UniformInt(80, 240)
                });
            }

            var num6 = i * 16 + 8;
            var num7 = j * 16 + 8;
            var num8 = 0;
            while (num8 < list.Count)
            {
                var cavePoint = list[num8];
                var list2 = _caveBrushesByType[cavePoint.BrushType];
                list2[random.UniformInt(0, list2.Count - 1)].PaintFastAvoidWater(chunk,
                    Terrain.ToCell(cavePoint.Position.X), Terrain.ToCell(cavePoint.Position.Y),
                    Terrain.ToCell(cavePoint.Position.Z));
                cavePoint.Position += 2f * cavePoint.Direction;
                cavePoint.StepsTaken += 2;
                var num9 = cavePoint.Position.X - num6;
                var num10 = cavePoint.Position.Z - num7;
                if (random.Bool(0.5f))
                {
                    var v3 = Vector3.Normalize(random.Vector3(1f, true));
                    if ((num9 < -25.5f && v3.X < 0f) || (num9 > 25.5f && v3.X > 0f))
                    {
                        v3.X = 0f - v3.X;
                    }

                    if ((num10 < -25.5f && v3.Z < 0f) || (num10 > 25.5f && v3.Z > 0f))
                    {
                        v3.Z = 0f - v3.Z;
                    }

                    if ((cavePoint.Direction.Y < -0.5f && v3.Y < -10f) || (cavePoint.Direction.Y > 0.1f && v3.Y > 0f))
                    {
                        v3.Y = 0f - v3.Y;
                    }

                    cavePoint.Direction = Vector3.Normalize(cavePoint.Direction + 0.5f * v3);
                }

                if (cavePoint.StepsTaken > 20 && random.Bool(0.06f))
                {
                    cavePoint.Direction = Vector3.Normalize(random.Vector3(1f, true) * new Vector3(1f, 0.33f, 1f));
                }

                if (cavePoint.StepsTaken > 20 && random.Bool(0.05f))
                {
                    cavePoint.Direction.Y = 0f;
                    cavePoint.BrushType = MathUtils.Min(cavePoint.BrushType + 2, _caveBrushesByType.Count - 1);
                }

                if (cavePoint.StepsTaken > 30 && random.Bool(0.03f))
                {
                    cavePoint.Direction.X = 0f;
                    cavePoint.Direction.Y = -1f;
                    cavePoint.Direction.Z = 0f;
                }

                if (cavePoint.StepsTaken > 30 && cavePoint.Position.Y < 30f && random.Bool(0.02f))
                {
                    cavePoint.Direction.X = 0f;
                    cavePoint.Direction.Y = 1f;
                    cavePoint.Direction.Z = 0f;
                }

                if (random.Bool(0.33f))
                {
                    cavePoint.BrushType =
                        (int)(MathUtils.Pow(random.UniformFloat(0f, 0.999f), 7f) * _caveBrushesByType.Count);
                }

                if (random.Bool(0.06f) && list.Count < 12 && cavePoint.StepsTaken > 20 && cavePoint.Position.Y < 58f)
                {
                    list.Add(new CavePoint
                    {
                        Position = cavePoint.Position,
                        Direction = Vector3.Normalize(random.UniformVector3(1f, 1f) * new Vector3(1f, 0.33f, 1f)),
                        BrushType =
                            (int)(MathUtils.Pow(random.UniformFloat(0f, 0.999f), 7f) * _caveBrushesByType.Count),
                        Length = random.UniformInt(40, 180)
                    });
                }

                if (cavePoint.StepsTaken >= cavePoint.Length || MathUtils.Abs(num9) > 34f ||
                    MathUtils.Abs(num10) > 34f || cavePoint.Position.Y < 5f || cavePoint.Position.Y > 246f)
                {
                    num8++;
                }
                else if (cavePoint.StepsTaken % 20 == 0)
                {
                    var num11 = CalculateHeight(cavePoint.Position.X, cavePoint.Position.Z);
                    if (cavePoint.Position.Y > num11 + 1f)
                    {
                        num8++;
                    }
                }
            }
        }
    }

    public void GenerateTreesAndLogs(TerrainChunk chunk)
    {
        if (!_tgExtras)
        {
            return;
        }

        var terrain = SubsystemTerrain.Terrain;
        var x = chunk.Origin.X;
        var num = x + 16;
        var y = chunk.Origin.Y;
        var num2 = y + 16;
        var x2 = chunk.Coords.X;
        var y2 = chunk.Coords.Y;
        for (var i = x2; i <= x2; i++)
        for (var j = y2; j <= y2; j++)
        {
            var random = new Engine.Core.Random(_seed + i + 3943 * j);
            var humidity = CalculateHumidity(i * 16, j * 16);
            var temperature = CalculateTemperature(i * 16, j * 16);
            var num4 = MathUtils.Saturate((SimplexNoise.OctavedNoise(i, j, 0.1f, 2, 2f, 0.5f) - 0.25f) / 0.2f +
                                          (random.Bool(0.25f) ? 0.5f : 0f));
            var num5 = 0;
            if (num4 > 0.95f)
            {
                num5 = 1 + (random.Bool(0.25f) ? 1 : 0);
            }
            else if (num4 > 0.5f)
            {
                num5 = random.Bool(0.25f) ? 1 : 0;
            }

            var num6 = 0;
            for (var k = 0; k < 8; k++)
            {
                if (num6 >= num5)
                {
                    break;
                }

                var num7 = i * 16 + random.Int(0, 15);
                var num8 = j * 16 + random.Int(0, 15);
                var num9 = terrain.CalculateTopmostCellHeight(num7, num8);
                if (num9 < 66)
                {
                    continue;
                }

                var cellContentsFast = terrain.GetCellContentsFast(num7, num9, num8);
                if (cellContentsFast != 2 && cellContentsFast != 8)
                {
                    continue;
                }

                num9++;
                var num10 = random.Int(3, 7);
                var point = CellFace.FaceToPoint3(random.Int(0, 3));
                if (point.X < 0 && num7 - num10 + 1 < 0)
                {
                    point.X *= -1;
                }

                if (point.X > 0 && num7 + num10 - 1 > 15)
                {
                    point.X *= -1;
                }

                if (point.Z < 0 && num8 - num10 + 1 < 0)
                {
                    point.Z *= -1;
                }

                if (point.Z > 0 && num8 + num10 - 1 > 15)
                {
                    point.Z *= -1;
                }

                var flag = true;
                var flag2 = false;
                var flag3 = false;
                for (var l = 0; l < num10; l++)
                {
                    var num11 = num7 + point.X * l;
                    var num12 = num8 + point.Z * l;
                    if (num11 < x + 1 || num11 >= num - 1 || num12 < y + 1 || num12 >= num2 - 1)
                    {
                        flag = false;
                        break;
                    }

                    if (BlocksManager.Blocks[terrain.GetCellContentsFast(num11, num9, num12)].Collidable)
                    {
                        flag = false;
                        break;
                    }

                    if (BlocksManager.Blocks[terrain.GetCellContentsFast(num11, num9 - 1, num12)].Collidable)
                    {
                        if (l <= MathUtils.Max(num10 / 2, 0))
                        {
                            flag2 = true;
                        }

                        if (l >= MathUtils.Min(num10 / 2 + 1, num10 - 1))
                        {
                            flag3 = true;
                        }
                    }
                }

                if (!((flag && flag2) & flag3))
                {
                    continue;
                }

                var point2 = point.X != 0 ? new Point3(0, 0, 1) : new Point3(1, 0, 0);
                var treeType = PlantsManager.GenerateRandomTreeType(random,
                    temperature + SubsystemWeather.GetTemperatureAdjustmentAtHeight(num9), humidity, num9);
                if (treeType.HasValue)
                {
                    var treeTrunkValue = PlantsManager.GetTreeTrunkValue(treeType.Value);
                    treeTrunkValue = Terrain.ReplaceData(treeTrunkValue,
                        WoodBlock.SetCutFace(Terrain.ExtractData(treeTrunkValue), point.X != 0 ? 1 : 0));
                    var treeLeavesValue = PlantsManager.GetTreeLeavesValue(treeType.Value);
                    for (var m = 0; m < num10; m++)
                    {
                        var num13 = num7 + point.X * m;
                        var num14 = num8 + point.Z * m;
                        terrain.SetCellValueFast(num13, num9, num14, treeTrunkValue);
                        if (m > num10 / 2)
                        {
                            if (random.Bool(0.3f) && !BlocksManager
                                    .Blocks[terrain.GetCellContentsFast(num13 + point2.X, num9, num14 + point2.Z)]
                                    .Collidable)
                            {
                                terrain.SetCellValueFast(num13 + point2.X, num9, num14 + point2.Z, treeLeavesValue);
                            }

                            if (random.Bool(0.05f) && !BlocksManager
                                    .Blocks[terrain.GetCellContentsFast(num13 + point2.X, num9, num14 + point2.Z)]
                                    .Collidable)
                            {
                                terrain.SetCellValueFast(num13 + point2.X, num9, num14 + point2.Z, treeTrunkValue);
                            }

                            if (random.Bool(0.3f) && !BlocksManager
                                    .Blocks[terrain.GetCellContentsFast(num13 - point2.X, num9, num14 - point2.Z)]
                                    .Collidable)
                            {
                                terrain.SetCellValueFast(num13 - point2.X, num9, num14 - point2.Z, treeLeavesValue);
                            }

                            if (random.Bool(0.05f) && !BlocksManager
                                    .Blocks[terrain.GetCellContentsFast(num13 - point2.X, num9, num14 - point2.Z)]
                                    .Collidable)
                            {
                                terrain.SetCellValueFast(num13 - point2.X, num9, num14 - point2.Z, treeTrunkValue);
                            }

                            if (random.Bool(0.1f) && !BlocksManager
                                    .Blocks[terrain.GetCellContentsFast(num13, num9 + 1, num14)]
                                    .Collidable)
                            {
                                terrain.SetCellValueFast(num13, num9 + 1, num14, treeLeavesValue);
                            }
                        }
                    }
                }

                num6++;
            }

            var num15 = (int)(5f * num4);
            var num16 = 0;
            for (var n = 0; n < 32; n++)
            {
                if (num16 >= num15)
                {
                    break;
                }

                var randomX = i * 16 + random.Int(2, 13);
                var randomZ = j * 16 + random.Int(2, 13);
                var randomY = terrain.CalculateTopmostCellHeight(randomX, randomZ);
                if (randomY < 66)
                {
                    continue;
                }

                var cellContentsFast2 = terrain.GetCellContentsFast(randomX, randomY, randomZ);
                if (cellContentsFast2 != 2 && cellContentsFast2 != 8)
                {
                    continue;
                }

                randomY++;
                if (!BlocksManager.Blocks[terrain.GetCellContentsFast(randomX + 1, randomY, randomZ)].Collidable &&
                    !BlocksManager.Blocks[terrain.GetCellContentsFast(randomX - 1, randomY, randomZ)].Collidable &&
                    !BlocksManager.Blocks[terrain.GetCellContentsFast(randomX, randomY, randomZ + 1)].Collidable &&
                    !BlocksManager.Blocks[terrain.GetCellContentsFast(randomX, randomY, randomZ - 1)].Collidable)
                {
                    var treeType2 = PlantsManager.GenerateRandomTreeType(random,
                        temperature + SubsystemWeather.GetTemperatureAdjustmentAtHeight(randomY), humidity, randomY);
                    if (treeType2.HasValue)
                    {
                        var treeBrushes = PlantsManager.GetTreeBrushes(treeType2.Value);
                        var treeBrush = treeBrushes[random.Int(treeBrushes.Count)];
                        treeBrush.PaintFast(chunk, randomX, randomY, randomZ);
                    }

                    num16++;
                }
            }
        }
    }

    public void GenerateBedrockAndAir(TerrainChunk chunk)
    {
        var value = Terrain.MakeBlockValue(1);
        for (var i = 0; i < 16; i++)
        for (var j = 0; j < 16; j++)
        {
            var num = i + chunk.Origin.X;
            var num2 = j + chunk.Origin.Y;
            float num3 = 2 + (int)(4f * SimplexNoise.OctavedNoise(num, num2, 0.1f, 1, 1f, 1f));
            for (var k = 0; k < num3; k++)
            {
                chunk.SetCellValueFast(i, k, j, value);
            }

            chunk.SetCellValueFast(i, 255, j, 0);
        }
    }

    public void GenerateGrassAndPlants(TerrainChunk chunk)
    {
        if (!_tgExtras)
        {
            return;
        }

        var random = new Engine.Core.Random(_seed + chunk.Coords.X + 3943 * chunk.Coords.Y);
        for (var i = 0; i < 16; i++)
        for (var j = 0; j < 16; j++)
        for (var num = 254; num >= 0; num--)
        {
            var cellValueFast = chunk.GetCellValueFast(i, num, j);
            var num2 = Terrain.ExtractContents(cellValueFast);
            if (num2 != 0)
            {
                if (!(BlocksManager.Blocks[num2] is FluidBlock))
                {
                    var temperatureFast = chunk.GetTemperatureFast(i, j);
                    var humidityFast = chunk.GetHumidityFast(i, j);
                    var num3 = PlantsManager.GenerateRandomPlantValue(random, cellValueFast, temperatureFast,
                        humidityFast, num + 1);
                    if (num3 != 0)
                    {
                        chunk.SetCellValueFast(i, num + 1, j, num3);
                    }

                    if (num2 == 2)
                    {
                        chunk.SetCellValueFast(i, num, j, Terrain.MakeBlockValue(8, 0, 0));
                    }
                }

                break;
            }
        }
    }

    public void GenerateBottomSuckers(TerrainChunk chunk)
    {
        if (!_tgExtras)
        {
            return;
        }

        var random = new Random(_seed + chunk.Coords.X + 2210 * chunk.Coords.Y);
        for (var i = 0; i < 16; i++)
        for (var j = 0; j < 16; j++)
        {
            if (!random.Bool(0.2f))
            {
                continue;
            }

            var num = chunk.Origin.X + i;
            var num2 = chunk.Origin.Y + j;
            var temperatureFast = chunk.GetTemperatureFast(i, j);
            if (CalculateOceanShoreDistance(num, num2) > 10f)
            {
                continue;
            }

            var num3 = 0;
            for (var num4 = 254; num4 >= 0; num4--)
            {
                if (Terrain.ExtractContents(chunk.GetCellValueFast(i, num4, j)) == 18)
                {
                    num3++;
                    var face = random.UniformInt(0, 5);
                    var point = CellFace.FaceToPoint3(face);
                    if (i + point.X >= 0 && i + point.X < 16 && num4 + point.Y >= 0 && num4 + point.Y < 254 &&
                        j + point.Z >= 0 && j + point.Z < 16)
                    {
                        var cellValueFast = chunk.GetCellValueFast(i + point.X, num4 + point.Y, j + point.Z);
                        if (SubsystemBottomSuckerBlockBehavior.IsSupport(cellValueFast, CellFace.OppositeFace(face)))
                        {
                            var num5 = 0;
                            var num6 = 0.6f;
                            var num7 = 0.4f;
                            if (temperatureFast < 8)
                            {
                                num6 = 0.9f;
                                num7 = 0.1f;
                            }

                            if (num3 > 6)
                            {
                                num6 *= 0.25f;
                            }

                            if (num3 > 12)
                            {
                                num7 *= 0.5f;
                            }

                            if (num3 < 4)
                            {
                                num7 *= 0.5f;
                            }

                            if (num4 < 45)
                            {
                                num6 *= 0.1f;
                                num7 *= 0.1f;
                            }

                            var num8 = random.UniformFloat(0f, 1f);
                            num8 -= num6;
                            if (num5 == 0 && num8 < 0f)
                            {
                                num5 = 226;
                            }

                            num8 -= num7;
                            if (num5 == 0 && num8 < 0f)
                            {
                                num5 = 229;
                            }

                            if (num5 != 0)
                            {
                                var face2 = random.UniformInt(0, 3);
                                var data = BottomSuckerBlock.SetFace(BottomSuckerBlock.SetSubvariant(0, face2),
                                    CellFace.OppositeFace(face));
                                var value = Terrain.MakeBlockValue(num5, 0, data);
                                chunk.SetCellValueFast(i, num4, j, value);
                            }
                        }
                    }
                }
                else
                {
                    num3 = 0;
                }
            }
        }
    }

    public void GenerateCacti(TerrainChunk chunk)
    {
        if (!_tgExtras)
        {
            return;
        }

        var x = chunk.Coords.X;
        var y = chunk.Coords.Y;
        var random = new Random(_seed + x + 1991 * y);
        if (!random.Bool(0.5f))
        {
            return;
        }

        var num = random.UniformInt(0, MathUtils.Max(1, 1));
        for (var i = 0; i < num; i++)
        {
            var num2 = random.UniformInt(3, 12);
            var num3 = random.UniformInt(3, 12);
            var humidityFast = chunk.GetHumidityFast(num2, num3);
            var temperatureFast = chunk.GetTemperatureFast(num2, num3);
            if (humidityFast >= 6 || temperatureFast <= 8)
            {
                continue;
            }

            for (var j = 0; j < 8; j++)
            {
                var num4 = num2 + random.UniformInt(-2, 2);
                var num5 = num3 + random.UniformInt(-2, 2);
                for (var num6 = 251; num6 >= 0; num6--)
                {
                    switch (Terrain.ExtractContents(chunk.GetCellValueFast(num4, num6, num5)))
                    {
                        case 7:
                        {
                            for (var k = num6 + 1;
                                 k <= num6 + 3 && chunk.GetCellContentsFast(num4 + 1, k, num5) == 0 &&
                                 chunk.GetCellContentsFast(num4 - 1, k, num5) == 0 &&
                                 chunk.GetCellContentsFast(num4, k, num5 + 1) == 0 &&
                                 chunk.GetCellContentsFast(num4, k, num5 - 1) == 0;
                                 k++)
                            {
                                chunk.SetCellValueFast(num4, k, num5, Terrain.MakeBlockValue(127));
                            }

                            break;
                        }
                        case 0:
                            continue;
                    }

                    break;
                }
            }
        }
    }

    public void GeneratePumpkins(TerrainChunk chunk)
    {
        if (!_tgExtras)
        {
            return;
        }

        var x = chunk.Coords.X;
        var y = chunk.Coords.Y;
        var random = new Random(_seed + x + 1495 * y);
        if (!random.Bool(0.2f))
        {
            return;
        }

        var num = random.UniformInt(0, MathUtils.Max(1, 1));
        for (var i = 0; i < num; i++)
        {
            var num2 = random.UniformInt(1, 14);
            var num3 = random.UniformInt(1, 14);
            var humidityFast = chunk.GetHumidityFast(num2, num3);
            var temperatureFast = chunk.GetTemperatureFast(num2, num3);
            if (humidityFast < 10 || temperatureFast <= 6)
            {
                continue;
            }

            for (var j = 0; j < 5; j++)
            {
                var x2 = num2 + random.UniformInt(-1, 1);
                var z = num3 + random.UniformInt(-1, 1);
                for (var num4 = 254; num4 >= 0; num4--)
                {
                    switch (Terrain.ExtractContents(chunk.GetCellValueFast(x2, num4, z)))
                    {
                        case 8:
                            chunk.SetCellValueFast(x2, num4 + 1, z,
                                random.Bool(0.25f) ? Terrain.MakeBlockValue(244) : Terrain.MakeBlockValue(131));
                            break;
                        case 0:
                            continue;
                    }

                    break;
                }
            }
        }
    }

    public void GenerateKelp(TerrainChunk chunk)
    {
        if (!_tgExtras)
        {
            return;
        }

        var x = chunk.Coords.X;
        var y = chunk.Coords.Y;
        var random = new Random(0);
        var num = 0f;
        for (var i = 0; i < 9; i++)
        {
            var num2 = i % 3 - 1;
            var num3 = i / 3 - 1;
            random.Reset(_seed + x + num2 + 850 * (y + num3));
            if (random.Bool(0.2f))
            {
                num = MathUtils.Max(num, 0.025f);
                if (i == 4)
                {
                    num = MathUtils.Max(num, 0.1f);
                }
            }
        }

        if (num == 0f)
        {
            return;
        }

        random.Reset(_seed + x + 850 * y);
        var num4 = random.UniformInt(0, MathUtils.Max((int)(256f * num), 1));
        for (var j = 0; j < num4; j++)
        {
            var num5 = random.UniformInt(2, 13);
            var num6 = random.UniformInt(2, 13);
            var num7 = num5 + chunk.Origin.X;
            var num8 = num6 + chunk.Origin.Y;
            var num9 = random.UniformInt(10, 26);
            var num10 = 6;
            var flag = true;
            if (CalculateOceanShoreDistance(num7, num8) > 5f)
            {
                num10 = 4;
                flag = false;
            }

            if (num9 <= 0)
            {
                continue;
            }

            for (var k = 0; k < num9; k++)
            {
                var x2 = num5 + random.UniformInt(-2, 2);
                var z = num6 + random.UniformInt(-2, 2);
                var num11 = 0;
                for (var num12 = 254; num12 >= 0; num12--)
                {
                    var num13 = Terrain.ExtractContents(chunk.GetCellValueFast(x2, num12, z));
                    var block = BlocksManager.Blocks[num13];
                    if (num13 != 0)
                    {
                        if (!(block is WaterBlock))
                        {
                            if ((num13 == 2 || num13 == 7 || num13 == 72) && num11 >= 2)
                            {
                                var num14 = flag
                                    ? random.UniformInt(num11 - 2, num11 - 1)
                                    : random.UniformInt(num11 - 1, num11);
                                for (var l = 0; l < num14; l++)
                                {
                                    chunk.SetCellValueFast(x2, num12 + 1 + l, z, Terrain.MakeBlockValue(232));
                                }
                            }

                            break;
                        }

                        num11++;
                        if (num11 > num10)
                        {
                            break;
                        }
                    }
                }
            }
        }
    }

    public void GenerateSeagrass(TerrainChunk chunk)
    {
        if (!_tgExtras)
        {
            return;
        }

        var x = chunk.Coords.X;
        var y = chunk.Coords.Y;
        var random = new Random(_seed + x + 378 * y);
        for (var i = 0; i < 6; i++)
        {
            var num = random.UniformInt(1, 14);
            var num2 = random.UniformInt(1, 14);
            var num3 = chunk.Origin.X + num;
            var num4 = chunk.Origin.Y + num2;
            var flag = CalculateOceanShoreDistance(num3, num4) < 10f;
            var num5 = random.UniformInt(1, 3);
            for (var j = 0; j < num5; j++)
            {
                var x2 = num + random.UniformInt(-1, 1);
                var z = num2 + random.UniformInt(-1, 1);
                var num6 = 0;
                for (var num7 = 254; num7 >= 0; num7--)
                {
                    var num8 = Terrain.ExtractContents(chunk.GetCellValueFast(x2, num7, z));
                    switch (num8)
                    {
                        case 18:
                            num6++;
                            if (num6 <= 16)
                            {
                                continue;
                            }

                            break;
                        default:
                            if (num6 > 1 && (num8 == 2 || num8 == 7 || num8 == 72 || num8 == 3))
                            {
                                var x3 = !random.Bool(0.1f) ? 1 : 2;
                                x3 = flag ? MathUtils.Min(x3, num6 - 1) : MathUtils.Min(x3, num6);
                                for (var k = 0; k < x3; k++)
                                {
                                    chunk.SetCellValueFast(x2, num7 + 1 + k, z, Terrain.MakeBlockValue(233));
                                }
                            }

                            break;
                        case 0:
                            continue;
                    }

                    break;
                }
            }
        }
    }

    public void GenerateIvy(TerrainChunk chunk)
    {
        if (!_tgExtras)
        {
            return;
        }

        var random = new Random(_seed + chunk.Coords.X + 2191 * chunk.Coords.Y);
        var num = random.UniformInt(0, MathUtils.Max(12, 1));
        for (var i = 0; i < num; i++)
        {
            var num2 = random.UniformInt(4, 11);
            var num3 = random.UniformInt(4, 11);
            var humidityFast = chunk.GetHumidityFast(num2, num3);
            var temperatureFast = chunk.GetTemperatureFast(num2, num3);
            if (humidityFast <= 10 || temperatureFast <= 10)
            {
                continue;
            }

            var num4 = chunk.CalculateTopmostCellHeight(num2, num3);
            for (var j = 0; j < 100; j++)
            {
                var num5 = num2 + random.UniformInt(-3, 3);
                var num6 = MathUtils.Clamp(num4 + random.UniformInt(-10, 1), 1, 255);
                var num7 = num3 + random.UniformInt(-3, 3);
                switch (Terrain.ExtractContents(chunk.GetCellValueFast(num5, num6, num7)))
                {
                    case 2:
                    case 3:
                    case 8:
                    case 9:
                    case 12:
                    case 66:
                    case 67:
                    {
                        var num8 = random.UniformInt(0, 3);
                        for (var k = 0; k < 4; k++)
                        {
                            var face = (k + num8) % 4;
                            var point = CellFace.FaceToPoint3(face);
                            if (chunk.GetCellContentsFast(num5 + point.X, num6, num7 + point.Z) != 0)
                            {
                                continue;
                            }

                            var num9 = num6 - 1;
                            while (num9 >= 1 && chunk.GetCellContentsFast(num5 + point.X, num9, num7 + point.Z) == 0 &&
                                   chunk.GetCellContentsFast(num5, num9, num7) != 0)
                            {
                                num9--;
                            }

                            if (chunk.GetCellContentsFast(num5 + point.X, num9, num7 + point.Z) != 0)
                            {
                                break;
                            }

                            num9++;
                            var value = Terrain.MakeBlockValue(197, 0,
                                IvyBlock.SetFace(0, CellFace.OppositeFace(face)));
                            while (num9 >= 1 && chunk.GetCellContentsFast(num5 + point.X, num9, num7 + point.Z) == 0)
                            {
                                chunk.SetCellValueFast(num5 + point.X, num9, num7 + point.Z, value);
                                if (IvyBlock.IsGrowthStopCell(num5 + point.X, num9, num7 + point.Z))
                                {
                                    break;
                                }

                                num9--;
                            }

                            break;
                        }

                        break;
                    }
                }
            }
        }
    }

    public void GenerateTraps(TerrainChunk chunk)
    {
        if (!_tgExtras)
        {
            return;
        }

        var x = chunk.Coords.X;
        var y = chunk.Coords.Y;
        _ = SubsystemTerrain.Terrain;
        var random = new Random(_seed + x + 2113 * y);
        if (!random.Bool(0.15f) || !(CalculateOceanShoreDistance(chunk.Origin.X, chunk.Origin.Y) > 50f))
        {
            return;
        }

        var num = random.UniformInt(0, MathUtils.Max(2, 1));
        for (var i = 0; i < num; i++)
        {
            var num2 = random.UniformInt(2, 5);
            var num3 = random.UniformInt(2, 5);
            var num4 = random.UniformInt(1, 16 - num2 - 2);
            var num5 = random.UniformInt(1, 16 - num3 - 2);
            var flag = random.UniformFloat(0f, 1f) < 0.5f;
            var num6 = random.UniformInt(3, 5);
            int? num7 = null;
            var num8 = num4 - 1;
            while (true)
            {
                if (num8 < num4 + num2 + 1)
                {
                    for (var j = num5 - 1; j < num5 + num3 + 1; j++)
                    {
                        var num9 = chunk.CalculateTopmostCellHeight(num8, j);
                        var num10 = MathUtils.Max(num9 - 20, 5);
                        while (num9 >= num10 && chunk.GetCellContentsFast(num8, num9, j) != 8)
                        {
                            num9--;
                        }

                        if (num7.HasValue && num7 != num9)
                        {
                            break;
                        }

                        num7 = num9;
                        if (chunk.GetCellContentsFast(num8, num9, j) != 8)
                        {
                            break;
                        }
                    }

                    num8++;
                    continue;
                }

                if (!num7.HasValue || num7 - num6 < 5)
                {
                    break;
                }

                for (var k = num4; k < num4 + num2; k++)
                for (var l = num5; l < num5 + num3; l++)
                {
                    for (var num11 = num7.Value - 1; num11 >= num7 - num6 + 1; num11--)
                    {
                        chunk.SetCellValueFast(k, num11, l, Terrain.MakeBlockValue(0));
                    }

                    chunk.SetCellValueFast(k, num7.Value, l, Terrain.MakeBlockValue(87));
                    if (flag)
                    {
                        var data = SpikedPlankBlock.SetSpikesState(0, random.UniformFloat(0f, 1f) < 0.33f);
                        chunk.SetCellValueFast(k, num7.Value - num6 + 1, l, Terrain.MakeBlockValue(86, 0, data));
                    }
                }

                break;
            }
        }
    }

    public void GenerateGraves(TerrainChunk chunk)
    {
        if (!_tgExtras)
        {
            return;
        }

        var x = chunk.Coords.X;
        var y = chunk.Coords.Y;
        var random = new Random((int)MathUtils.Hash((uint)(_seed + x + 10323 * y)));
        if (!(random.UniformFloat(0f, 1f) < 0.033f) ||
            !(CalculateOceanShoreDistance(chunk.Origin.X, chunk.Origin.Y) > 10f))
        {
            return;
        }

        var num = random.UniformInt(0, MathUtils.Max(1, 1));
        for (var i = 0; i < num; i++)
        {
            var num2 = random.UniformInt(6, 9);
            var num3 = random.UniformInt(6, 9);
            var num4 = random.Bool(0.2f) ? random.UniformInt(6, 20) : random.UniformInt(1, 5);
            var flag = random.Bool(0.5f);
            for (var j = 0; j < num4; j++)
            {
                var num5 = num2 + random.UniformInt(-4, 4);
                var num6 = num3 + random.UniformInt(-4, 4);
                var num7 = chunk.CalculateTopmostCellHeight(num5, num6);
                if (num7 < 10 || num7 > 246)
                {
                    continue;
                }

                var num8 = random.UniformInt(0, 3);
                for (var k = 0; k < 4; k++)
                {
                    var num9 = (k + num8) % 4;
                    var p = CellFace.FaceToPoint3(num9);
                    var p2 = new Point3(-p.Z, p.Y, p.X);
                    var num10 = p.X < 0 ? num5 - 2 : num5 - 1;
                    var num11 = p.X > 0 ? num5 + 2 : num5 + 1;
                    var num12 = p.Z < 0 ? num6 - 2 : num6 - 1;
                    var num13 = p.Z > 0 ? num6 + 2 : num6 + 1;
                    for (var l = num10; l <= num11; l++)
                    for (var m = num7 - 2; m <= num7 + 2; m++)
                    for (var n = num12; n <= num13; n++)
                    {
                        var num14 = Terrain.ExtractContents(chunk.GetCellValueFast(l, m, n));
                        var block = BlocksManager.Blocks[num14];
                        if (m > num7)
                        {
                            if (!block.Collidable)
                            {
                                continue;
                            }
                        }
                        else if (num14 == 8 || num14 == 2 || num14 == 7 || num14 == 3 || num14 == 4)
                        {
                            continue;
                        }

                        goto IL_06ac;
                    }

                    var num15 = random.UniformInt(0, 7);
                    var data = GravestoneBlock.SetVariant(GravestoneBlock.SetRotation(0, num9 % 2), num15);
                    int? num16 = null;
                    var contents = 217;
                    var contents2 = 136;
                    if (num15 >= 4 && !flag)
                    {
                        var cellContentsFast = chunk.GetCellContentsFast(num5, num7, num6);
                        if (cellContentsFast == 7 || cellContentsFast == 4)
                        {
                            num16 = Terrain.MakeBlockValue(4);
                            contents = 51;
                            contents2 = 52;
                        }
                        else if (random.UniformFloat(0f, 1f) < 0.5f)
                        {
                            num16 = Terrain.MakeBlockValue(3);
                            contents = 217;
                            contents2 = 136;
                        }
                        else
                        {
                            num16 = Terrain.MakeBlockValue(67);
                            contents = 96;
                            contents2 = 95;
                        }
                    }

                    var flag2 = num16.HasValue && random.Bool(0.33f);
                    var num17 = random.UniformFloat(0f, 1f);
                    var num18 = random.UniformFloat(0f, 1f);
                    var num19 = random.UniformInt(-1, 0);
                    var num20 = random.UniformInt(1, 2);
                    var num21 = flag2 ? num7 + 2 : num7 + 1;
                    chunk.SetCellValueFast(num5, num21, num6, Terrain.MakeBlockValue(189, 0, data));
                    for (var num22 = num19; num22 <= num20; num22++)
                    {
                        var num23 = num5 + p.X * num22;
                        var num24 = num6 + p.Z * num22;
                        if (num22 == 0 || num22 == 1)
                        {
                            chunk.SetCellValueFast(num23, num21 - 2, num24, Terrain.MakeBlockValue(190));
                            if (num16.HasValue)
                            {
                                chunk.SetCellValueFast(num23, num21 - 1, num24, num16.Value);
                                if (num22 == 1)
                                {
                                    var num25 = 0;
                                    if (num18 < 0.2f)
                                    {
                                        num25 = Terrain.MakeBlockValue(20);
                                    }
                                    else if (num18 < 0.3f)
                                    {
                                        num25 = Terrain.MakeBlockValue(24);
                                    }
                                    else if (num18 < 0.4f)
                                    {
                                        num25 = Terrain.MakeBlockValue(25);
                                    }
                                    else if (num18 < 0.5f)
                                    {
                                        num25 = Terrain.MakeBlockValue(31, 0, 4);
                                    }
                                    else if (num18 < 0.6f)
                                    {
                                        num25 = Terrain.MakeBlockValue(132, 0, CellFace.OppositeFace(num9));
                                    }

                                    if (num25 != 0)
                                    {
                                        chunk.SetCellValueFast(num23, num21, num24, num25);
                                    }
                                }
                            }
                        }

                        if (!flag2)
                        {
                            continue;
                        }

                        if (num17 < 0.3f)
                        {
                            var value = Terrain.MakeBlockValue(contents, 0,
                                StairsBlock.SetRotation(0, CellFace.Point3ToFace(p2)));
                            var value2 = Terrain.MakeBlockValue(contents, 0,
                                StairsBlock.SetRotation(0, CellFace.OppositeFace(CellFace.Point3ToFace(p2))));
                            chunk.SetCellValueFast(num23 + p2.X, num21 - 1, num24 + p2.Z, value);
                            chunk.SetCellValueFast(num23 - p2.X, num21 - 1, num24 - p2.Z, value2);
                            if (num22 == -1)
                            {
                                var value3 = Terrain.MakeBlockValue(contents, 0,
                                    StairsBlock.SetRotation(0, CellFace.OppositeFace(CellFace.Point3ToFace(p))));
                                chunk.SetCellValueFast(num23, num21 - 1, num24, value3);
                            }

                            if (num22 == 2)
                            {
                                var value4 = Terrain.MakeBlockValue(contents, 0,
                                    StairsBlock.SetRotation(0, CellFace.Point3ToFace(p)));
                                chunk.SetCellValueFast(num23, num21 - 1, num24, value4);
                            }
                        }
                        else if (num17 < 0.4f)
                        {
                            chunk.SetCellValueFast(num23 + p2.X, num21 - 1, num24 + p2.Z,
                                Terrain.MakeBlockValue(contents2));
                            chunk.SetCellValueFast(num23 - p2.X, num21 - 1, num24 - p2.Z,
                                Terrain.MakeBlockValue(contents2));
                            if (num22 == -1)
                            {
                                chunk.SetCellValueFast(num23, num21 - 1, num24, Terrain.MakeBlockValue(contents2));
                            }

                            if (num22 == 2)
                            {
                                chunk.SetCellValueFast(num23, num21 - 1, num24, Terrain.MakeBlockValue(contents2));
                            }
                        }
                        else if (num17 < 0.6f)
                        {
                            if (num22 == 0 || num22 == 1)
                            {
                                chunk.SetCellValueFast(num23 + p2.X, num21 - 1, num24 + p2.Z,
                                    Terrain.MakeBlockValue(31, 0, CellFace.Point3ToFace(p2)));
                                chunk.SetCellValueFast(num23 - p2.X, num21 - 1, num24 - p2.Z,
                                    Terrain.MakeBlockValue(31, 0, CellFace.OppositeFace(CellFace.Point3ToFace(p2))));
                            }

                            if (num22 == -1)
                            {
                                chunk.SetCellValueFast(num23, num21 - 1, num24,
                                    Terrain.MakeBlockValue(31, 0, CellFace.OppositeFace(num9)));
                            }

                            if (num22 == 2)
                            {
                                chunk.SetCellValueFast(num23, num21 - 1, num24, Terrain.MakeBlockValue(31, 0, num9));
                            }
                        }
                    }

                    break;
                    IL_06ac: ;
                }
            }
        }
    }

    public void GenerateSnowAndIce(TerrainChunk chunk)
    {
        for (var i = 0; i < 16; i++)
        for (var j = 0; j < 16; j++)
        {
            var num = i + chunk.Origin.X;
            var num2 = j + chunk.Origin.Y;
            for (var num3 = 254; num3 >= 0; num3--)
            {
                var cellValueFast = chunk.GetCellValueFast(i, num3, j);
                var num4 = Terrain.ExtractContents(cellValueFast);
                if (num4 != 0)
                {
                    if (!SubsystemWeather.IsPlaceFrozen(chunk.GetTemperatureFast(i, j), num3))
                    {
                        break;
                    }

                    if (BlocksManager.Blocks[num4] is WaterBlock)
                    {
                        if (CalculateOceanShoreDistance(num, num2) > -20f)
                        {
                            float num5 = 1 +
                                         (int)(2f * MathUtils.Sqr(SimplexNoise.OctavedNoise(num, num2, 0.2f, 1, 2f,
                                             1f)));
                            for (var k = 0; k < num5; k++)
                            {
                                if (num3 - k > 0)
                                {
                                    if (!(BlocksManager.Blocks[
                                            chunk.GetCellContentsFast(i, num3 - k, j)] is WaterBlock))
                                    {
                                        break;
                                    }

                                    chunk.SetCellValueFast(i, num3 - k, j, 62);
                                }
                            }

                            if (SubsystemWeather.ShaftHasSnowOnIce(num, num2))
                            {
                                chunk.SetCellValueFast(i, num3 + 1, j, 61);
                            }
                        }
                    }
                    else if (SubsystemSnowBlockBehavior.CanSupportSnow(cellValueFast))
                    {
                        chunk.SetCellValueFast(i, num3 + 1, j, 61);
                    }

                    if (num4 == 8)
                    {
                        chunk.SetCellValueFast(i, num3, j, Terrain.MakeBlockValue(8, 0, 1));
                    }

                    break;
                }
            }
        }
    }

    public void PropagateFluidsDownwards(TerrainChunk chunk)
    {
        for (var i = 0; i < 16; i++)
        for (var j = 0; j < 16; j++)
        {
            var num = TerrainChunk.CalculateCellIndex(i, 255, j);
            var num2 = 0;
            var num3 = 255;
            while (num3 >= 0)
            {
                var num4 = Terrain.ExtractContents(chunk.GetCellValueFast(num));
                if (num4 == 0 && num2 != 0 && BlocksManager.FluidBlocks[num2] != null)
                {
                    chunk.SetCellValueFast(num, num2);
                    num4 = num2;
                }

                num2 = num4;
                num3--;
                num--;
            }
        }
    }

    public void UpdateFluidIsTop(TerrainChunk chunk)
    {
        for (var i = 0; i < 16; i++)
        for (var j = 0; j < 16; j++)
        {
            var num = TerrainChunk.CalculateCellIndex(i, 255, j);
            var num2 = 0;
            var num3 = 255;
            while (num3 >= 0)
            {
                var cellValueFast = chunk.GetCellValueFast(num);
                var num4 = Terrain.ExtractContents(cellValueFast);
                if (num4 != num2 && BlocksManager.FluidBlocks[num4] != null && BlocksManager.FluidBlocks[num2] == null)
                {
                    var data = Terrain.ExtractData(cellValueFast);
                    chunk.SetCellValueFast(num, Terrain.MakeBlockValue(num4, 0, FluidBlock.SetIsTop(data, true)));
                }

                num2 = num4;
                num3--;
                num--;
            }
        }
    }

    public static void CreateBrushes()
    {
        var random = new Random(17);
        for (var i = 0; i < 16; i++)
        {
            var terrainBrush = new TerrainBrush();
            var num = random.UniformInt(4, 12);
            for (var j = 0; j < num; j++)
            {
                var vector = 0.5f * Vector3.Normalize(new Vector3(random.UniformFloat(-1f, 1f),
                    random.UniformFloat(-1f, 1f), random.UniformFloat(-1f, 1f)));
                var num2 = random.UniformInt(3, 8);
                var zero = Vector3.Zero;
                for (var k = 0; k < num2; k++)
                {
                    terrainBrush.AddBox((int)MathUtils.Floor(zero.X), (int)MathUtils.Floor(zero.Y),
                        (int)MathUtils.Floor(zero.Z), 1, 1, 1, 16);
                    zero += vector;
                }
            }

            if (i == 0)
            {
                terrainBrush.AddCell(0, 0, 0, 150);
            }

            terrainBrush.Compile();
            _coalBrushes.Add(terrainBrush);
        }

        for (var l = 0; l < 16; l++)
        {
            var terrainBrush2 = new TerrainBrush();
            var num3 = random.UniformInt(3, 7);
            for (var m = 0; m < num3; m++)
            {
                var vector2 = 0.5f * Vector3.Normalize(new Vector3(random.UniformFloat(-1f, 1f),
                    random.UniformFloat(-1f, 1f), random.UniformFloat(-1f, 1f)));
                var num4 = random.UniformInt(3, 6);
                var zero2 = Vector3.Zero;
                for (var n = 0; n < num4; n++)
                {
                    terrainBrush2.AddBox((int)MathUtils.Floor(zero2.X), (int)MathUtils.Floor(zero2.Y),
                        (int)MathUtils.Floor(zero2.Z), 1, 1, 1, 39);
                    zero2 += vector2;
                }
            }

            terrainBrush2.Compile();
            _ironBrushes.Add(terrainBrush2);
        }

        for (var num5 = 0; num5 < 16; num5++)
        {
            var terrainBrush3 = new TerrainBrush();
            var num6 = random.UniformInt(4, 10);
            for (var num7 = 0; num7 < num6; num7++)
            {
                var vector3 = 0.5f * Vector3.Normalize(new Vector3(random.UniformFloat(-1f, 1f),
                    random.UniformFloat(-2f, 2f), random.UniformFloat(-1f, 1f)));
                var num8 = random.UniformInt(3, 6);
                var zero3 = Vector3.Zero;
                for (var num9 = 0; num9 < num8; num9++)
                {
                    terrainBrush3.AddBox((int)MathUtils.Floor(zero3.X), (int)MathUtils.Floor(zero3.Y),
                        (int)MathUtils.Floor(zero3.Z), 1, 1, 1, 41);
                    zero3 += vector3;
                }
            }

            terrainBrush3.Compile();
            _copperBrushes.Add(terrainBrush3);
        }

        for (var num10 = 0; num10 < 16; num10++)
        {
            var terrainBrush4 = new TerrainBrush();
            var num11 = random.UniformInt(8, 16);
            for (var num12 = 0; num12 < num11; num12++)
            {
                var vector4 = 0.5f * Vector3.Normalize(new Vector3(random.UniformFloat(-1f, 1f),
                    random.UniformFloat(-0.25f, 0.25f), random.UniformFloat(-1f, 1f)));
                var num13 = random.UniformInt(4, 8);
                var zero4 = Vector3.Zero;
                for (var num14 = 0; num14 < num13; num14++)
                {
                    terrainBrush4.AddBox((int)MathUtils.Floor(zero4.X), (int)MathUtils.Floor(zero4.Y),
                        (int)MathUtils.Floor(zero4.Z), 1, 1, 1, 100);
                    zero4 += vector4;
                }
            }

            terrainBrush4.Compile();
            _saltpeterBrushes.Add(terrainBrush4);
        }

        for (var num15 = 0; num15 < 16; num15++)
        {
            var terrainBrush5 = new TerrainBrush();
            var num16 = random.UniformInt(4, 10);
            for (var num17 = 0; num17 < num16; num17++)
            {
                var vector5 = 0.5f * Vector3.Normalize(new Vector3(random.UniformFloat(-1f, 1f),
                    random.UniformFloat(-1f, 1f), random.UniformFloat(-1f, 1f)));
                var num18 = random.UniformInt(3, 6);
                var zero5 = Vector3.Zero;
                for (var num19 = 0; num19 < num18; num19++)
                {
                    terrainBrush5.AddBox((int)MathUtils.Floor(zero5.X), (int)MathUtils.Floor(zero5.Y),
                        (int)MathUtils.Floor(zero5.Z), 1, 1, 1, 101);
                    zero5 += vector5;
                }
            }

            terrainBrush5.Compile();
            _sulphurBrushes.Add(terrainBrush5);
        }

        for (var num20 = 0; num20 < 16; num20++)
        {
            var terrainBrush6 = new TerrainBrush();
            var num21 = random.UniformInt(2, 6);
            for (var num22 = 0; num22 < num21; num22++)
            {
                var vector6 = 0.5f * Vector3.Normalize(new Vector3(random.UniformFloat(-1f, 1f),
                    random.UniformFloat(-1f, 1f), random.UniformFloat(-1f, 1f)));
                var num23 = random.UniformInt(3, 6);
                var zero6 = Vector3.Zero;
                for (var num24 = 0; num24 < num23; num24++)
                {
                    terrainBrush6.AddBox((int)MathUtils.Floor(zero6.X), (int)MathUtils.Floor(zero6.Y),
                        (int)MathUtils.Floor(zero6.Z), 1, 1, 1, 112);
                    zero6 += vector6;
                }
            }

            terrainBrush6.Compile();
            _diamondBrushes.Add(terrainBrush6);
        }

        for (var num25 = 0; num25 < 16; num25++)
        {
            var terrainBrush7 = new TerrainBrush();
            var num26 = random.UniformInt(4, 10);
            for (var num27 = 0; num27 < num26; num27++)
            {
                var vector7 = 0.5f * Vector3.Normalize(new Vector3(random.UniformFloat(-1f, 1f),
                    random.UniformFloat(-1f, 1f), random.UniformFloat(-1f, 1f)));
                var num28 = random.UniformInt(3, 6);
                var zero7 = Vector3.Zero;
                for (var num29 = 0; num29 < num28; num29++)
                {
                    terrainBrush7.AddBox((int)MathUtils.Floor(zero7.X), (int)MathUtils.Floor(zero7.Y),
                        (int)MathUtils.Floor(zero7.Z), 1, 1, 1, 148);
                    zero7 += vector7;
                }
            }

            terrainBrush7.Compile();
            _germaniumBrushes.Add(terrainBrush7);
        }

        for (var num30 = 0; num30 < 16; num30++)
        {
            var terrainBrush8 = new TerrainBrush();
            var num31 = random.UniformInt(16, 32);
            for (var num32 = 0; num32 < num31; num32++)
            {
                var vector8 = 0.5f * Vector3.Normalize(new Vector3(random.UniformFloat(-1f, 1f),
                    random.UniformFloat(-0.75f, 0.75f), random.UniformFloat(-1f, 1f)));
                var num33 = random.UniformInt(6, 12);
                var zero8 = Vector3.Zero;
                for (var num34 = 0; num34 < num33; num34++)
                {
                    terrainBrush8.AddBox((int)MathUtils.Floor(zero8.X), (int)MathUtils.Floor(zero8.Y),
                        (int)MathUtils.Floor(zero8.Z), 1, 1, 1, 2);
                    zero8 += vector8;
                }
            }

            terrainBrush8.Compile();
            _dirtPocketBrushes.Add(terrainBrush8);
        }

        for (var num35 = 0; num35 < 16; num35++)
        {
            var terrainBrush9 = new TerrainBrush();
            var num36 = random.UniformInt(16, 32);
            for (var num37 = 0; num37 < num36; num37++)
            {
                var vector9 = 0.5f * Vector3.Normalize(new Vector3(random.UniformFloat(-1f, 1f),
                    random.UniformFloat(-0.75f, 0.75f), random.UniformFloat(-1f, 1f)));
                var num38 = random.UniformInt(6, 12);
                var zero9 = Vector3.Zero;
                for (var num39 = 0; num39 < num38; num39++)
                {
                    terrainBrush9.AddBox((int)MathUtils.Floor(zero9.X), (int)MathUtils.Floor(zero9.Y),
                        (int)MathUtils.Floor(zero9.Z), 1, 1, 1, 6);
                    zero9 += vector9;
                }
            }

            terrainBrush9.Compile();
            _gravelPocketBrushes.Add(terrainBrush9);
        }

        for (var num40 = 0; num40 < 16; num40++)
        {
            var terrainBrush10 = new TerrainBrush();
            var num41 = random.UniformInt(16, 32);
            for (var num42 = 0; num42 < num41; num42++)
            {
                var vector10 = 0.5f * Vector3.Normalize(new Vector3(random.UniformFloat(-1f, 1f),
                    random.UniformFloat(-0.75f, 0.75f), random.UniformFloat(-1f, 1f)));
                var num43 = random.UniformInt(6, 12);
                var zero10 = Vector3.Zero;
                for (var num44 = 0; num44 < num43; num44++)
                {
                    terrainBrush10.AddBox((int)MathUtils.Floor(zero10.X), (int)MathUtils.Floor(zero10.Y),
                        (int)MathUtils.Floor(zero10.Z), 1, 1, 1, 66);
                    zero10 += vector10;
                }
            }

            terrainBrush10.Compile();
            _limestonePocketBrushes.Add(terrainBrush10);
        }

        for (var num45 = 0; num45 < 16; num45++)
        {
            var terrainBrush11 = new TerrainBrush();
            var num46 = random.UniformInt(16, 32);
            for (var num47 = 0; num47 < num46; num47++)
            {
                var vector11 = 0.5f * Vector3.Normalize(new Vector3(random.UniformFloat(-1f, 1f),
                    random.UniformFloat(-0.1f, 0.1f), random.UniformFloat(-1f, 1f)));
                var num48 = random.UniformInt(6, 12);
                var zero11 = Vector3.Zero;
                for (var num49 = 0; num49 < num48; num49++)
                {
                    terrainBrush11.AddBox((int)MathUtils.Floor(zero11.X), (int)MathUtils.Floor(zero11.Y),
                        (int)MathUtils.Floor(zero11.Z), 1, 1, 1, 72);
                    zero11 += vector11;
                }
            }

            terrainBrush11.Compile();
            _clayPocketBrushes.Add(terrainBrush11);
        }

        for (var num50 = 0; num50 < 16; num50++)
        {
            var terrainBrush12 = new TerrainBrush();
            var num51 = random.UniformInt(16, 32);
            for (var num52 = 0; num52 < num51; num52++)
            {
                var vector12 = 0.5f * Vector3.Normalize(new Vector3(random.UniformFloat(-1f, 1f),
                    random.UniformFloat(-0.75f, 0.75f), random.UniformFloat(-1f, 1f)));
                var num53 = random.UniformInt(6, 12);
                var zero12 = Vector3.Zero;
                for (var num54 = 0; num54 < num53; num54++)
                {
                    terrainBrush12.AddBox((int)MathUtils.Floor(zero12.X), (int)MathUtils.Floor(zero12.Y),
                        (int)MathUtils.Floor(zero12.Z), 1, 1, 1, 7);
                    zero12 += vector12;
                }
            }

            terrainBrush12.Compile();
            _sandPocketBrushes.Add(terrainBrush12);
        }

        for (var num55 = 0; num55 < 16; num55++)
        {
            var terrainBrush13 = new TerrainBrush();
            var num56 = random.UniformInt(16, 32);
            for (var num57 = 0; num57 < num56; num57++)
            {
                var vector13 = 0.5f * Vector3.Normalize(new Vector3(random.UniformFloat(-1f, 1f),
                    random.UniformFloat(-0.75f, 0.75f), random.UniformFloat(-1f, 1f)));
                var num58 = random.UniformInt(6, 12);
                var zero13 = Vector3.Zero;
                for (var num59 = 0; num59 < num58; num59++)
                {
                    terrainBrush13.AddBox((int)MathUtils.Floor(zero13.X), (int)MathUtils.Floor(zero13.Y),
                        (int)MathUtils.Floor(zero13.Z), 1, 1, 1, 67);
                    zero13 += vector13;
                }
            }

            terrainBrush13.Compile();
            _basaltPocketBrushes.Add(terrainBrush13);
        }

        for (var num60 = 0; num60 < 16; num60++)
        {
            var terrainBrush14 = new TerrainBrush();
            var num61 = random.UniformInt(16, 32);
            for (var num62 = 0; num62 < num61; num62++)
            {
                var vector14 = 0.5f * Vector3.Normalize(new Vector3(random.UniformFloat(-1f, 1f),
                    random.UniformFloat(-1f, 1f), random.UniformFloat(-1f, 1f)));
                var num63 = random.UniformInt(5, 10);
                var zero14 = Vector3.Zero;
                for (var num64 = 0; num64 < num63; num64++)
                {
                    terrainBrush14.AddBox((int)MathUtils.Floor(zero14.X), (int)MathUtils.Floor(zero14.Y),
                        (int)MathUtils.Floor(zero14.Z), 1, 1, 1, 3);
                    zero14 += vector14;
                }
            }

            terrainBrush14.Compile();
            _granitePocketBrushes.Add(terrainBrush14);
        }

        var array = new[]
        {
            4,
            6,
            8
        };
        for (var num65 = 0; num65 < 4 * array.Length; num65++)
        {
            var terrainBrush15 = new TerrainBrush();
            var num66 = array[num65 / 4];
            var num67 = num65 % 2 + 1;
            var num68 = num65 % 4 == 2 ? 0.5f : 1f;
            var circular = num65 % 4 >= 2;
            var num69 = num65 % 4 == 1 ? num66 * num66 : 2 * num66 * num66;
            for (var num70 = 0; num70 < num69; num70++)
            {
                var vector15 = random.UniformVector2(0f, num66, circular);
                var num71 = vector15.Length();
                var num72 = random.UniformInt(3, 4);
                var sizeY = 1 + (int)MathUtils.Lerp(MathUtils.Max(num66 / 3, 2.5f) * num68, 0f, num71 / num66) +
                            random.UniformInt(0, 1);
                terrainBrush15.AddBox((int)MathUtils.Floor(vector15.X), 0, (int)MathUtils.Floor(vector15.Y), num72,
                    sizeY, num72, 0);
                terrainBrush15.AddBox((int)MathUtils.Floor(vector15.X), -num67, (int)MathUtils.Floor(vector15.Y), num72,
                    num67, num72, 18);
            }

            terrainBrush15.Compile();
            _waterPocketBrushes.Add(terrainBrush15);
        }

        var array2 = new int[]
        {
            8,
            12,
            14,
            16
        };
        for (var num73 = 0; num73 < 4 * array2.Length; num73++)
        {
            var terrainBrush16 = new TerrainBrush();
            var num74 = array2[num73 / 4];
            var num75 = num74 + 2;
            var num76 = num73 % 4 == 2 ? 0.5f : 1f;
            var circular2 = num73 % 4 >= 2;
            var num77 = num73 % 4 == 1 ? num74 * num74 : 2 * num74 * num74;
            for (var num78 = 0; num78 < num77; num78++)
            {
                var vector16 = random.UniformVector2(0f, num74, circular2);
                var num79 = vector16.Length();
                var num80 = random.UniformInt(3, 4);
                var sizeY2 = 1 + (int)MathUtils.Lerp(MathUtils.Max(num74 / 3, 2.5f) * num76, 0f, num79 / num74) +
                             random.UniformInt(0, 1);
                var num81 = 1 + (int)MathUtils.Lerp(num75, 0f, num79 / num74) + random.UniformInt(0, 1);
                terrainBrush16.AddBox((int)MathUtils.Floor(vector16.X), 0, (int)MathUtils.Floor(vector16.Y), num80,
                    sizeY2, num80, 0);
                terrainBrush16.AddBox((int)MathUtils.Floor(vector16.X), -num81, (int)MathUtils.Floor(vector16.Y), num80,
                    num81, num80, 92);
            }

            terrainBrush16.Compile();
            _magmaPocketBrushes.Add(terrainBrush16);
        }

        for (var num82 = 0; num82 < 7; num82++)
        {
            _caveBrushesByType.Add(new List<TerrainBrush>());
            for (var num83 = 0; num83 < 3; num83++)
            {
                var terrainBrush17 = new TerrainBrush();
                var num84 = 6 + 4 * num82;
                var max = 3 + num82 / 3;
                var max2 = 9 + num82;
                for (var num85 = 0; num85 < num84; num85++)
                {
                    var num86 = random.UniformInt(2, max);
                    var num87 = random.UniformInt(8, max2) - 2 * num86;
                    var vector17 = 0.5f * new Vector3(random.UniformFloat(-1f, 1f), random.UniformFloat(0f, 1f),
                        random.UniformFloat(-1f, 1f));
                    var zero15 = Vector3.Zero;
                    for (var num88 = 0; num88 < num87; num88++)
                    {
                        terrainBrush17.AddBox((int)MathUtils.Floor(zero15.X) - num86 / 2,
                            (int)MathUtils.Floor(zero15.Y) - num86 / 2, (int)MathUtils.Floor(zero15.Z) - num86 / 2,
                            num86, num86, num86, 0);
                        zero15 += vector17;
                    }
                }

                terrainBrush17.Compile();
                _caveBrushesByType[num82].Add(terrainBrush17);
            }
        }
    }

    public class Random
    {
        private const ulong _multiplier = 25214903917uL;

        private const ulong _addend = 11uL;

        private const ulong _mask = 281474976710655uL;
        private static int _counter = (int)Stopwatch.GetTimestamp();

        public static readonly Random GlobalRandom = new(0);

        private ulong _seed;

        public Random()
            : this(997 * _counter++)
        {
        }

        public Random(int seed)
        {
            Reset(seed);
        }

        public void Reset(int seed)
        {
            _seed = (ulong)(seed ^ 0x5DEECE66D);
        }

        public int Sign()
        {
            if (Int() % 2 != 0)
            {
                return 1;
            }

            return -1;
        }

        public bool Bool()
        {
            return Int() % 2 == 0;
        }

        public bool Bool(float probability)
        {
            return Int() / 2.147484E+09f < probability;
        }

        public int Int()
        {
            _seed = (_seed * 25214903917L + 11) & 0xFFFFFFFFFFFF;
            return (int)(_seed >> 17);
        }

        public int UniformInt(int min, int max)
        {
            return (int)(min + Int() * (long)(max - min + 1) / 2147483648L);
        }

        public float UniformFloat(float min, float max)
        {
            var num = Int() / 2.147484E+09f;
            return min + num * (max - min);
        }

        public float NormalFloat(float mean, float stddev)
        {
            var num = UniformFloat(0f, 1f);
            if (num < 0.5)
            {
                var num2 = MathUtils.Sqrt(-2f * MathUtils.Log(num));
                var num3 = 0.322232425f +
                           num2 * (1f + num2 * (0.3422421f + num2 * (0.0204231218f + num2 * 4.536422E-05f)));
                var num4 = 0.09934846f +
                           num2 * (0.588581562f + num2 * (0.5311035f + num2 * (0.103537753f + num2 * 0.00385607f)));
                return mean + stddev * (num3 / num4 - num2);
            }

            var num5 = MathUtils.Sqrt(-2f * MathUtils.Log(1f - num));
            var num6 = 0.322232425f + num5 * (1f + num5 * (0.3422421f + num5 * (0.0204231218f + num5 * 4.536422E-05f)));
            var num7 = 0.09934846f +
                       num5 * (0.588581562f + num5 * (0.5311035f + num5 * (0.103537753f + num5 * 0.00385607f)));
            return mean - stddev * (num6 / num7 - num5);
        }

        public Vector2 Vector2(float length, bool circular = false)
        {
            Vector2 v;
            float num;
            do
            {
                v = new Vector2(UniformFloat(-1f, 1f), UniformFloat(-1f, 1f));
                num = v.LengthSquared();
            } while (circular && num > 1f);

            return v * (length / MathUtils.Sqrt(num));
        }

        public Vector2 UniformVector2(float minLength, float maxLength, bool circular = false)
        {
            Vector2 v;
            float num;
            do
            {
                v = new Vector2(UniformFloat(-1f, 1f), UniformFloat(-1f, 1f));
                num = v.LengthSquared();
            } while (circular && num > 1f);

            return v * (UniformFloat(minLength, maxLength) / MathUtils.Sqrt(num));
        }

        public Vector3 Vector3(float length, bool spherical = false)
        {
            Vector3 v;
            float num;
            do
            {
                v = new Vector3(UniformFloat(-1f, 1f), UniformFloat(-1f, 1f), UniformFloat(-1f, 1f));
                num = v.LengthSquared();
            } while (spherical && num > 1f);

            return v * (length / MathUtils.Sqrt(num));
        }

        public Vector3 UniformVector3(float minLength, float maxLength, bool spherical = false)
        {
            Vector3 v;
            float num;
            do
            {
                v = new Vector3(UniformFloat(-1f, 1f), UniformFloat(-1f, 1f), UniformFloat(-1f, 1f));
                num = v.LengthSquared();
            } while (spherical && num > 1f);

            return v * (UniformFloat(minLength, maxLength) / MathUtils.Sqrt(num));
        }
    }

    public class OldRandom
    {
        private static int _seed = (int)Stopwatch.GetTimestamp();

        public static readonly OldRandom GlobalRandom = new(0);

        private InternalRandom _random;

        public OldRandom()
        {
            _random = new InternalRandom(997 * _seed++);
        }

        public OldRandom(int seed)
        {
            _random = new InternalRandom(seed);
        }

        public int Sign()
        {
            if (_random.Next() % 2 != 0)
            {
                return 1;
            }

            return -1;
        }

        public bool Bool()
        {
            return _random.Next() % 2 == 0;
        }

        public int UniformInt(int min, int max)
        {
            return _random.Next(min, max + 1);
        }

        public float UniformFloat(float min, float max)
        {
            return (float)_random.NextDouble() * (max - min) + min;
        }

        public float NormalFloat(float mean, float stddev)
        {
            var num = UniformFloat(0f, 1f);
            if (num < 0.5)
            {
                var num2 = MathUtils.Sqrt(-2f * MathUtils.Log(num));
                var num3 = 0.322232425f +
                           num2 * (1f + num2 * (0.3422421f + num2 * (0.0204231218f + num2 * 4.536422E-05f)));
                var num4 = 0.09934846f +
                           num2 * (0.588581562f + num2 * (0.5311035f + num2 * (0.103537753f + num2 * 0.00385607f)));
                return mean + stddev * (num3 / num4 - num2);
            }

            var num5 = MathUtils.Sqrt(-2f * MathUtils.Log(1f - num));
            var num6 = 0.322232425f + num5 * (1f + num5 * (0.3422421f + num5 * (0.0204231218f + num5 * 4.536422E-05f)));
            var num7 = 0.09934846f +
                       num5 * (0.588581562f + num5 * (0.5311035f + num5 * (0.103537753f + num5 * 0.00385607f)));
            return mean - stddev * (num6 / num7 - num5);
        }

        public Vector2 Vector2(float length)
        {
            return Engine.Core.Vector2.Normalize(new Vector2(UniformFloat(-1f, 1f), UniformFloat(-1f, 1f))) * length;
        }

        public Vector2 UniformVector2(float minLength, float maxLength)
        {
            return Engine.Core.Vector2.Normalize(new Vector2(UniformFloat(-1f, 1f), UniformFloat(-1f, 1f))) *
                   UniformFloat(minLength, maxLength);
        }

        public Vector3 Vector3(float length)
        {
            return Engine.Core.Vector3.Normalize(new Vector3(UniformFloat(-1f, 1f), UniformFloat(-1f, 1f),
                UniformFloat(-1f, 1f))) * length;
        }

        public Vector3 UniformVector3(float minLength, float maxLength)
        {
            return Engine.Core.Vector3.Normalize(new Vector3(UniformFloat(-1f, 1f), UniformFloat(-1f, 1f),
                UniformFloat(-1f, 1f))) * UniformFloat(minLength, maxLength);
        }

        public class InternalRandom
        {
            private int _inext;

            private int _inextp;

            private int[] _seedArray;

            public InternalRandom(int seed)
            {
                _seedArray = new int[56];
                var num = seed == -2147483648 ? 2147483647 : Math.Abs(seed);
                var num2 = 161803398 - num;
                _seedArray[55] = num2;
                var num3 = 1;
                for (var i = 1; i < 55; i++)
                {
                    var num4 = 21 * i % 55;
                    _seedArray[num4] = num3;
                    num3 = num2 - num3;
                    if (num3 < 0)
                    {
                        num3 += 2147483647;
                    }

                    num2 = _seedArray[num4];
                }

                for (var j = 1; j < 5; j++)
                for (var k = 1; k < 56; k++)
                {
                    _seedArray[k] -= _seedArray[1 + (k + 30) % 55];
                    if (_seedArray[k] < 0)
                    {
                        _seedArray[k] += 2147483647;
                    }
                }

                _inext = 0;
                _inextp = 21;
            }

            public double GetSampleForLargeRange()
            {
                var num = InternalSample();
                if (InternalSample() % 2 == 0)
                {
                    num = -num;
                }

                return (num + 2147483646.0) / 4294967293.0;
            }

            public int InternalSample()
            {
                var inext = _inext;
                var inextp = _inextp;
                if (++inext >= 56)
                {
                    inext = 1;
                }

                if (++inextp >= 56)
                {
                    inextp = 1;
                }

                var num = _seedArray[inext] - _seedArray[inextp];
                if (num == 2147483647)
                {
                    num--;
                }

                if (num < 0)
                {
                    num += 2147483647;
                }

                _seedArray[inext] = num;
                _inext = inext;
                _inextp = inextp;
                return num;
            }

            public int Next()
            {
                return InternalSample();
            }

            public int Next(int maxValue)
            {
                if (maxValue < 0)
                {
                    throw new ArgumentOutOfRangeException("maxValue");
                }

                return (int)(Sample() * maxValue);
            }

            public int Next(int minValue, int maxValue)
            {
                if (minValue > maxValue)
                {
                    throw new ArgumentOutOfRangeException("minValue");
                }

                long num = maxValue - minValue;
                if (num <= 2147483647)
                {
                    return (int)(Sample() * num) + minValue;
                }

                return (int)(long)(GetSampleForLargeRange() * num) + minValue;
            }

            public void NextBytes(byte[] buffer)
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException("buffer");
                }

                for (var i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = (byte)(InternalSample() % 256);
                }
            }

            public double NextDouble()
            {
                return Sample();
            }

            public double Sample()
            {
                return InternalSample() * 4.6566128752457969E-10;
            }
        }
    }

    public class CavePoint
    {
        public int BrushType;

        public Vector3 Direction;

        public int Length;
        public Vector3 Position;

        public int StepsTaken;
    }

    public class Grid2D
    {
        private readonly float[] _data;

        private readonly int _sizeX;

        private readonly int _sizeY;

        public Grid2D(int sizeX, int sizeY)
        {
            _sizeX = sizeX;
            _sizeY = sizeY;
            _data = new float[_sizeX * _sizeY];
        }

        public int SizeX => _sizeX;

        public int SizeY => _sizeY;

        public float Get(int x, int y)
        {
            return _data[x + y * _sizeX];
        }

        public void Set(int x, int y, float value)
        {
            _data[x + y * _sizeX] = value;
        }

        public float Sample(float x, float y)
        {
            var num = (int)MathUtils.Floor(x);
            var num2 = (int)MathUtils.Floor(y);
            var num3 = (int)MathUtils.Ceiling(x);
            var num4 = (int)MathUtils.Ceiling(y);
            var f = x - num;
            var f2 = y - num2;
            var x2 = _data[num + num2 * _sizeX];
            var x3 = _data[num3 + num2 * _sizeX];
            var x4 = _data[num + num4 * _sizeX];
            var x5 = _data[num3 + num4 * _sizeX];
            var x6 = MathUtils.Lerp(x2, x3, f);
            var x7 = MathUtils.Lerp(x4, x5, f);
            return MathUtils.Lerp(x6, x7, f2);
        }
    }

    public class Grid3D
    {
        private readonly float[] _data;

        private readonly int _sizeX;

        private readonly int _sizeXY;

        private readonly int _sizeY;

        private readonly int _sizeZ;

        public Grid3D(int sizeX, int sizeY, int sizeZ)
        {
            _sizeX = sizeX;
            _sizeY = sizeY;
            _sizeZ = sizeZ;
            _sizeXY = _sizeX * _sizeY;
            _data = new float[_sizeX * _sizeY * _sizeZ];
        }

        public int SizeX => _sizeX;

        public int SizeY => _sizeY;

        public int SizeZ => _sizeZ;

        public void Get8(int x, int y, int z, out float v111, out float v211, out float v121, out float v221,
            out float v112, out float v212, out float v122, out float v222)
        {
            var num = x + y * _sizeX + z * _sizeXY;
            v111 = _data[num];
            v211 = _data[num + 1];
            v121 = _data[num + _sizeX];
            v221 = _data[num + 1 + _sizeX];
            v112 = _data[num + _sizeXY];
            v212 = _data[num + 1 + _sizeXY];
            v122 = _data[num + _sizeX + _sizeXY];
            v222 = _data[num + 1 + _sizeX + _sizeXY];
        }

        public float Get(int x, int y, int z)
        {
            return _data[x + y * _sizeX + z * _sizeXY];
        }

        public void Set(int x, int y, int z, float value)
        {
            _data[x + y * _sizeX + z * _sizeXY] = value;
        }

        public float Sample(float x, float y, float z)
        {
            var num = (int)MathUtils.Floor(x);
            var num2 = (int)MathUtils.Ceiling(x);
            var num3 = (int)MathUtils.Floor(y);
            var num4 = (int)MathUtils.Ceiling(y);
            var num5 = (int)MathUtils.Floor(z);
            var num6 = (int)MathUtils.Ceiling(z);
            var f = x - num;
            var f2 = y - num3;
            var f3 = z - num5;
            var x2 = _data[num + num3 * _sizeX + num5 * _sizeX * _sizeY];
            var x3 = _data[num2 + num3 * _sizeX + num5 * _sizeX * _sizeY];
            var x4 = _data[num + num4 * _sizeX + num5 * _sizeX * _sizeY];
            var x5 = _data[num2 + num4 * _sizeX + num5 * _sizeX * _sizeY];
            var x6 = _data[num + num3 * _sizeX + num6 * _sizeX * _sizeY];
            var x7 = _data[num2 + num3 * _sizeX + num6 * _sizeX * _sizeY];
            var x8 = _data[num + num4 * _sizeX + num6 * _sizeX * _sizeY];
            var x9 = _data[num2 + num4 * _sizeX + num6 * _sizeX * _sizeY];
            var x10 = MathUtils.Lerp(x2, x3, f);
            var x11 = MathUtils.Lerp(x4, x5, f);
            var x12 = MathUtils.Lerp(x6, x7, f);
            var x13 = MathUtils.Lerp(x8, x9, f);
            var x14 = MathUtils.Lerp(x10, x11, f2);
            var x15 = MathUtils.Lerp(x12, x13, f2);
            return MathUtils.Lerp(x14, x15, f3);
        }
    }
}
