using System.Collections.Concurrent;

namespace Game.TerrainSerializers;

public class TerrainContentsGenerator23 : ITerrainContentsGenerator
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

    /// <summary>
    /// 山脉细节噪声频率（静态全局参数）
    /// </summary>
    private static float _tgMountainsDetailFreq;

    /// <summary>
    /// 山脉细节噪声迭代次数（静态全局参数）
    /// </summary>
    private static int _tgMountainsDetailOctaves;

    /// <summary>
    /// 山脉细节噪声持久度（静态全局参数）
    /// </summary>
    private static float _tgMountainsDetailPersistence;

    /// <summary>
    /// 地形表面高度乘数（全局缩放）
    /// </summary>
    private static float _tgSurfaceMultiplier;

    private readonly Vector2 _humidityOffset;

    private readonly Vector2? _islandSize;

    private readonly Vector2 _mountainsOffset;

    private readonly Vector2 _oceanCorner;

    private readonly Vector2 _riversOffset;

    private readonly int _seed;

    private readonly SubsystemBottomSuckerBlockBehavior _subsystemBottomSuckerBlockBehavior;

    private readonly SubsystemTerrain _subsystemTerrain;

    private readonly Terrain _terrain;

    private readonly Vector2 _temperatureOffset;

    private readonly WorldSettings _worldSettings;

    /// <summary>
    /// 控制生物群落的规模比例（影响不同生态区域的分布范围）
    /// </summary>
    private readonly float _tgBiomeScaling;

    /// <summary>
    /// 是否生成地下洞穴和岩层孔洞
    /// </summary>
    private readonly bool _tgCavesAndPockets;

    /// <summary>
    /// 地形密度基准偏移量（正值为致密化，负值稀疏化）
    /// </summary>
    private readonly float _tgDensityBias;

    /// <summary>
    /// 是否生成特殊地形特征（如巨石/古树等）
    /// </summary>
    private readonly bool _tgExtras;

    /// <summary>
    /// 全局高度基准调整（正抬升/负降低整个地形）
    /// </summary>
    private readonly float _tgHeightBias;

    /// <summary>
    /// 丘陵地形的细节频率（值小产生大尺度起伏）
    /// </summary>
    private readonly float _tgHillsFrequency;

    /// <summary>
    /// 丘陵噪声的迭代次数（复杂度与性能消耗正相关）
    /// </summary>
    private readonly int _tgHillsOctaves;

    /// <summary>
    /// 丘陵区域占地百分比（0-1范围）
    /// </summary>
    private readonly float _tgHillsPercentage;

    /// <summary>
    /// 丘陵噪声的持久度（影响细节衰减率）
    /// </summary>
    private readonly float _tgHillsPersistence;

    /// <summary>
    /// 丘陵地形的起伏强度（影响垂直尺度）
    /// </summary>
    private readonly float _tgHillsStrength;

    /// <summary>
    /// 调整岛屿的生成密度（值越高岛屿数量越多）
    /// </summary>
    private readonly float _tgIslandsFrequency;

    /// <summary>
    /// 湍流扰动的最小阈值
    /// </summary>
    private readonly float _tgMinTurbulence;

    /// <summary>
    /// 山脉分布的空间频率
    /// </summary>
    private readonly float _tgMountainRangeFreq;

    /// <summary>
    /// 山脉区域占地百分比（0-1范围）
    /// </summary>
    private readonly float _tgMountainsPercentage;

    /// <summary>
    /// 山脉的最大隆起强度
    /// </summary>
    private readonly float _tgMountainsStrength;

    /// <summary>
    /// 定义海洋地形的倾斜角度（影响浅滩到深海的过渡梯度）
    /// </summary>
    private readonly float _tgOceanSlope;

    /// <summary>
    /// 控制海洋坡度的随机变化幅度
    /// </summary>
    private readonly float _tgOceanSlopeVariation;

    /// <summary>
    /// 河流侵蚀作用强度（影响河道深度）
    /// </summary>
    private readonly float _tgRiversStrength;

    /// <summary>
    /// 控制海岸线的不规则程度（值越大海岸线越曲折）
    /// </summary>
    private readonly float _tgShoreFluctuations;

    /// <summary>
    /// 调节海岸线波动的缩放比例（影响细节层次）
    /// </summary>
    private readonly float _tgShoreFluctuationsScaling;

    /// <summary>
    /// 湍流噪声的基础频率
    /// </summary>
    private readonly float _tgTurbulenceFreq;

    /// <summary>
    /// 湍流噪声的迭代次数
    /// </summary>
    private readonly int _tgTurbulenceOctaves;

    /// <summary>
    /// 湍流噪声的持久度
    /// </summary>
    private readonly float _tgTurbulencePersistence;

    /// <summary>
    /// 地形湍流扰动强度（值越大地形越破碎）
    /// </summary>
    private readonly float _tgTurbulenceStrength;

    /// <summary>
    /// 湍力归零的基准线位置
    /// </summary>
    private readonly float _tgTurbulenceZero;

    private readonly ConcurrentDictionary<(int Seed, Point2 Coords), CleanTreeBrushCacheEntry> _cleanTreeBrushCache =
        new();

    private long _cleanTreeBrushCleanupTicks;

    private long _cleanTreeBrushRequestCount;

    private const int _cleanTreeBrushCacheCleanupInterval = 256;

    private const int _cleanTreeBrushCacheMaxEntries = 4096;

    private const long _cleanTreeBrushCacheMaxAgeMs = 10 * 60 * 1000;

    static TerrainContentsGenerator23()
    {
        CreateBrushes();
    }

    public TerrainContentsGenerator23(SubsystemTerrain subsystemTerrain, Terrain? terrain = null)
    {
        _subsystemTerrain = subsystemTerrain;
        _terrain = terrain ?? subsystemTerrain.Terrain;
        _subsystemBottomSuckerBlockBehavior =
            subsystemTerrain.Project.FindSubsystem<SubsystemBottomSuckerBlockBehavior>(true)!;
        var subsystemGameInfo = subsystemTerrain.Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _worldSettings = subsystemGameInfo.WorldSettings;
        _seed = subsystemGameInfo.WorldSeed;
        _islandSize = TerrainGenerationModes.IsIsland(_worldSettings.TerrainGenerationMode)
            ? new Vector2?(_worldSettings.IslandSize)
            : null;
        var random = new Random(_seed);
        var num = _islandSize.HasValue ? MathUtils.Min(_islandSize.Value.X, _islandSize.Value.Y) : float.MaxValue;
        _oceanCorner = new Vector2(-200f, -200f);
        _temperatureOffset = new Vector2(random.Float(-3000f, 3000f), random.Float(-3000f, 3000f));
        _humidityOffset = new Vector2(random.Float(-3000f, 3000f), random.Float(-3000f, 3000f));
        _mountainsOffset = new Vector2(random.Float(-3000f, 3000f), random.Float(-3000f, 3000f));
        _riversOffset = new Vector2(random.Float(-3000f, 3000f), random.Float(-3000f, 3000f));
        _tgBiomeScaling = (TerrainGenerationModes.IsIsland(_worldSettings.TerrainGenerationMode) ? 1f : 1.75f) *
                          _worldSettings.BiomeSize;
        _tgShoreFluctuations = MathUtils.Clamp(2f * num, 0f, 150f);
        _tgShoreFluctuationsScaling = MathUtils.Clamp(0.04f * num, 0.5f, 3f);
        _tgOceanSlope = 0.006f;
        _tgOceanSlopeVariation = 0.004f;
        _tgIslandsFrequency = 0.01f;
        _tgDensityBias = 55f;
        _tgHeightBias = 1f;
        _tgRiversStrength = 1f;
        _tgMountainsStrength = 220f;
        _tgMountainRangeFreq = 0.0006f;
        _tgMountainsPercentage = 0.13f;
        _tgMountainsDetailFreq = 0.003f;
        _tgMountainsDetailOctaves = 3;
        _tgMountainsDetailPersistence = 0.53f;
        _tgHillsPercentage = 0.3f;
        _tgHillsStrength = 32f;
        _tgHillsOctaves = 1;
        _tgHillsFrequency = 0.014f;
        _tgHillsPersistence = 0.5f;
        _tgTurbulenceStrength = 55f;
        _tgTurbulenceFreq = 0.03f;
        _tgTurbulenceOctaves = 1;
        _tgTurbulencePersistence = 0.5f;
        _tgMinTurbulence = 0.04f;
        _tgTurbulenceZero = 0.84f;
        _tgSurfaceMultiplier = 2f;
        _tgExtras = true;
        _tgCavesAndPockets = true;
    }

    public int OceanLevel => 64 + _worldSettings.SeaLevelOffset;

    public Vector3 FindCoarseSpawnPosition()
    {
        var vector = Vector2.Zero;
        var num = float.MinValue;
        var random = new Random();
        for (var i = 0; i < 1000; i += 5)
        for (var j = 0; j <= 12; j += 4)
        for (var k = 0; k < 2; k++)
        {
            float num2;
            float x;
            if (k == 0)
            {
                num2 = _oceanCorner.Y + i;
                x = CalculateOceanShoreX(num2) + j + random.Int(-100, 100);
            }
            else
            {
                x = _oceanCorner.X + i;
                num2 = CalculateOceanShoreZ(x) + j + random.Int(-100, 100);
            }

            var num3 = ScoreSpawnPosition(Terrain.ToCell(x), Terrain.ToCell(num2));
            if (!(num3 > num))
            {
                continue;
            }

            vector = new Vector2(x, num2);
            num = num3;
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
        RegisterCleanTreeBrush(chunk);
        GenerateGrassAndPlants(chunk);
        GenerateLogs(chunk);
        GenerateTrees(chunk);
        GenerateCacti(chunk);
        GeneratePumpkins(chunk);
        GenerateKelp(chunk);
        GenerateSeagrass(chunk);
        GenerateBottomSuckers(chunk);
        GenerateTraps(chunk);
        GenerateIvy(chunk);
        GenerateGraves(chunk);
        GenerateCairns(chunk);
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
        return SimplexNoise.OctavedNoise(x + _mountainsOffset.X, z + _mountainsOffset.Y,
            _tgMountainRangeFreq / _tgBiomeScaling, 3, 1.91f, 0.75f, true);
    }

    public float CalculateHeight(float x, float z)
    {
        var num = _tgOceanSlope + _tgOceanSlopeVariation * MathUtils.PowSign(
            2f * SimplexNoise.OctavedNoise(x + _mountainsOffset.X, z + _mountainsOffset.Y, 0.01f, 1, 2f, 0.5f) - 1f,
            0.5f);
        var num2 = CalculateOceanShoreDistance(x, z);
        var num3 = MathUtils.Saturate(2f - 0.05f * MathUtils.Abs(num2));
        var num4 = MathUtils.Saturate(MathUtils.Sin(_tgIslandsFrequency * num2));
        var num5 = MathUtils.Saturate(MathUtils.Saturate((0f - num) * num2) - 0.85f * num4);
        var num6 = MathUtils.Saturate(MathUtils.Saturate(0.05f * (0f - num2 - 10f)) - num4);
        var v = CalculateMountainRangeFactor(x, z);
        var f = (1f - num3) * SimplexNoise.OctavedNoise(x, z, 0.001f / _tgBiomeScaling, 2, 2f, 0.5f);
        var f2 = (1f - num3) * SimplexNoise.OctavedNoise(x, z, 0.0017f / _tgBiomeScaling, 2, 4f, 0.7f);
        var num7 = (1f - num6) * (1f - num3) * Squish(v, 1f - _tgHillsPercentage, 1f - _tgMountainsPercentage);
        var num8 = (1f - num6) * Squish(v, 1f - _tgMountainsPercentage, 1f);
        var num9 = 1f * SimplexNoise.OctavedNoise(x, z, _tgHillsFrequency, _tgHillsOctaves, 1.93f, _tgHillsPersistence);
        var amplitudeStep =
            MathUtils.Lerp(0.75f * _tgMountainsDetailPersistence, 1.33f * _tgMountainsDetailPersistence, f);
        var num10 = 1.5f * SimplexNoise.OctavedNoise(x, z, _tgMountainsDetailFreq, _tgMountainsDetailOctaves, 1.98f,
            amplitudeStep) - 0.5f;
        var num11 = MathUtils.Lerp(60f, 30f,
            MathUtils.Saturate(1f * num8 + 0.5f * num7 + MathUtils.Saturate(1f - num2 / 30f)));
        var x2 = MathUtils.Lerp(-2f, -4f, MathUtils.Saturate(num8 + 0.5f * num7));
        var num12 = MathUtils.Saturate(1.5f - num11 *
            MathUtils.Abs(2f *
                SimplexNoise.OctavedNoise(x + _riversOffset.X, z + _riversOffset.Y, 0.001f, 4, 2f, 0.5f) - 1f));
        var num13 = -50f * num5 + _tgHeightBias;
        var num14 = MathUtils.Lerp(0f, 8f, f);
        var num15 = MathUtils.Lerp(0f, -6f, f2);
        var num16 = _tgHillsStrength * num7 * num9;
        var num17 = _tgMountainsStrength * num8 * num10;
        var f3 = _tgRiversStrength * num12;
        var num18 = num13 + num14 + num15 + num17 + num16;
        var num19 = MathUtils.Min(MathUtils.Lerp(num18, x2, f3), num18);
        return MathUtils.Clamp(64f + num19, 10f, 251f);
    }

    public int CalculateTemperature(float x, float z)
    {
        return MathUtils.Clamp(
            (int)(MathUtils.Saturate(
                3f * SimplexNoise.OctavedNoise(x + _temperatureOffset.X, z + _temperatureOffset.Y,
                    0.0015f / _tgBiomeScaling, 5, 2f, 0.6f) - 1.1f + _worldSettings.TemperatureOffset / 16f) * 16f), 0,
            15);
    }

    public int CalculateHumidity(float x, float z)
    {
        return MathUtils.Clamp(
            (int)(MathUtils.Saturate(
                3f * SimplexNoise.OctavedNoise(x + _humidityOffset.X, z + _humidityOffset.Y, 0.0012f / _tgBiomeScaling,
                    5, 2f, 0.6f) - 0.9f + _worldSettings.HumidityOffset / 16f) * 16f), 0, 15);
    }

    public static float Squish(float v, float zero, float one)
    {
        return MathUtils.Saturate((v - zero) / (one - zero));
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
        var num = CalculateTemperature(x, z);
        var num2 = CalculateHumidity(x, z);
        var num3 = CalculateMountainRangeFactor(x, z);
        var num4 = CalculateHeight(x, z);
        var x2 = CalculateHeight(x - 5, z - 5);
        var x3 = CalculateHeight(x - 5, z + 5);
        var x4 = CalculateHeight(x + 5, z - 5);
        var x5 = CalculateHeight(x + 5, z + 5);
        var num5 = MathUtils.Min(num4, MathUtils.Max(x2, x3, x4, x5));
        var num6 = MathUtils.Max(num4, MathUtils.Max(x2, x3, x4, x5));
        var num7 = num6 - num5;
        var num8 = 0f;
        if (MathUtils.Max(MathUtils.Abs(x), MathUtils.Abs(z)) > 400)
        {
            num8 -= 0.25f;
        }

        if (num4 < 65f)
        {
            num8 -= 1f;
        }

        if (num4 < 66f)
        {
            num8 -= 0.5f;
        }

        if (num3 > 0.9f)
        {
            num8 -= 1f;
        }

        if (num3 > 0.85f)
        {
            num8 -= 0.5f;
        }

        if (num3 > 0.8f)
        {
            num8 -= 0.25f;
        }

        switch (_subsystemTerrain.SubsystemGameInfo.WorldSettings.StartingPositionMode)
        {
            case StartingPositionMode.Easy:
                if (num < 9)
                {
                    num8 -= 0.5f;
                }

                if (num < 7)
                {
                    num8 -= 1f;
                }

                if (num2 is > 2 and < 10)
                {
                    num8 -= 0.5f;
                }

                if (num6 > 75f)
                {
                    num8 -= 1f;
                }

                if (num7 > 5f)
                {
                    num8 -= 1f;
                }

                break;
            case StartingPositionMode.Medium:
                if (num < 3)
                {
                    num8 -= 0.5f;
                }

                if (num > 6)
                {
                    num8 -= 1f;
                }

                if (num2 is > 3 and < 8)
                {
                    num8 -= 0.5f;
                }

                if (num2 > 10)
                {
                    num8 -= 0.5f;
                }

                if (num6 > 80f)
                {
                    num8 -= 1f;
                }

                if (num7 > 10f)
                {
                    num8 -= 1f;
                }

                break;
            default:
                if (num > 0)
                {
                    num8 -= 0.5f;
                }

                if (num > 2)
                {
                    num8 -= 1f;
                }

                if (num2 > 6)
                {
                    num8 -= 0.5f;
                }

                if (num2 > 8)
                {
                    num8 -= 1f;
                }

                if (num6 > 85f)
                {
                    num8 -= 1f;
                }

                if (num7 > 15f)
                {
                    num8 -= 1f;
                }

                if (num7 < 5f)
                {
                    num8 -= 0.5f;
                }

                break;
        }

        return num8;
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
            var v = CalculateMountainRangeFactor(num5, num6);
            var num8 = MathUtils.Lerp(_tgMinTurbulence, 1f, Squish(v, _tgTurbulenceZero, 1f));
            for (var m = 0; m < grid3D.SizeY; m++)
            {
                var num9 = m * 8;
                var num10 = _tgTurbulenceStrength * num8 * MathUtils.Saturate(num7 - num9) * (2f *
                    SimplexNoise.OctavedNoise(num5, num9, num6, _tgTurbulenceFreq, _tgTurbulenceOctaves, 4f,
                        _tgTurbulencePersistence) - 1f);
                var num11 = num9 + num10;
                var num12 = num7 - num11;
                num12 += MathUtils.Max(4f * (_tgDensityBias - num9), 0f);
                grid3D.Set(k, m, l, num12);
            }
        }

        var oceanLevel = OceanLevel;
        for (var n = 0; n < grid3D.SizeX - 1; n++)
        for (var num13 = 0; num13 < grid3D.SizeZ - 1; num13++)
        for (var num14 = 0; num14 < grid3D.SizeY - 1; num14++)
        {
            grid3D.Get8(n, num14, num13, out var v2, out var v3, out var v4, out var v5, out var v6, out var v7,
                out var v8, out var v9);
            var num15 = (v3 - v2) / 4f;
            var num16 = (v5 - v4) / 4f;
            var num17 = (v7 - v6) / 4f;
            var num18 = (v9 - v8) / 4f;
            var num19 = v2;
            var num20 = v4;
            var num21 = v6;
            var num22 = v8;
            for (var num23 = 0; num23 < 4; num23++)
            {
                var num24 = (num21 - num19) / 4f;
                var num25 = (num22 - num20) / 4f;
                var num26 = num19;
                var num27 = num20;
                for (var num28 = 0; num28 < 4; num28++)
                {
                    var num29 = (num27 - num26) / 8f;
                    var num30 = num26;
                    var num31 = num23 + n * 4;
                    var num32 = num28 + num13 * 4;
                    var x3 = x1 + num31;
                    var z3 = z1 + num32;
                    var x4 = grid2D.Get(num31, num32);
                    var num33 = grid2D2.Get(num31, num32);
                    var temperatureFast = chunk.GetTemperatureFast(x3, z3);
                    var humidityFast = chunk.GetHumidityFast(x3, z3);
                    var f = num33 - 0.01f * humidityFast;
                    var num34 = MathUtils.Lerp(100f, 0f, f);
                    var num35 = MathUtils.Lerp(300f, 30f, f);
                    var flag = (temperatureFast > 8 && humidityFast < 8 && num33 < 0.97f) ||
                               (MathUtils.Abs(x4) < 16f && num33 < 0.97f);
                    var num36 = TerrainChunk.CalculateCellIndex(x3, 0, z3);
                    for (var num37 = 0; num37 < 8; num37++)
                    {
                        var num38 = num37 + num14 * 8;
                        var value = 0;
                        if (num30 < 0f)
                        {
                            if (num38 <= oceanLevel)
                            {
                                value = 18;
                            }
                        }
                        else
                        {
                            value = !flag ? !(num30 < num35) ? 67 : 3 :
                                !(num30 < num34) ? !(num30 < num35) ? 67 : 3 : 4;
                        }

                        chunk.SetCellValueFast(num36 + num38, value);
                        num30 += num29;
                    }

                    num26 += num24;
                    num27 += num25;
                }

                num19 += num15;
                num20 += num16;
                num21 += num17;
                num22 += num18;
            }
        }
    }

    public void GenerateSurface(TerrainChunk chunk)
    {
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
                    var temperature = chunk.GetTemperatureFast(i, j);
                    var humidity = chunk.GetHumidityFast(i, j);
                    int num7;
                    if (num5 == 4)
                    {
                        num7 = temperature > 4 && temperature < 7 ? 6 : 7;
                    }
                    else
                    {
                        var num8 = temperature / 4;
                        var num9 = num4 + 1 < 255 ? chunk.GetCellContentsFast(i, num4 + 1, j) : 0;
                        num7 = num4 > 120 && SubsystemWeather.IsPlaceFrozen(temperature, num4) ? 62 :
                            (num4 < 66 || num4 == 84 + num8 || num4 == 103 + num8) && humidity == 9 &&
                            temperature % 6 == 1 ? 66 :
                            num9 != 18 || humidity <= 8 || humidity % 2 != 0 || temperature % 3 != 0 ? 2 : 72;
                    }

                    int num10;
                    if (num7 == 62)
                    {
                        num10 = (int)MathUtils.Clamp(1f * -temperature, 1f, 7f);
                    }
                    else
                    {
                        var num11 = MathUtils.Saturate((num4 - 100f) * 0.05f);
                        var f = MathUtils.Saturate(MathUtils.Saturate((num6 - 0.9f) / 0.1f) -
                            MathUtils.Saturate((humidity - 3f) / 12f) + _tgSurfaceMultiplier * num11);
                        var min = (int)MathUtils.Lerp(4f, 0f, f);
                        var max = (int)MathUtils.Lerp(7f, 0f, f);
                        num10 = MathUtils.Min(random.Int(min, max), num4);
                    }

                    var num12 = TerrainChunk.CalculateCellIndex(i, num4 + 1, j);
                    for (var k = num12 - num10; k < num12; k++)
                    {
                        if (Terrain.ExtractContents(chunk.GetCellValueFast(k)) == 0)
                        {
                            continue;
                        }

                        var value = Terrain.ReplaceContents(0, num7);
                        chunk.SetCellValueFast(k, value);
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
            var num = random.Int(0, 10);
            for (var k = 0; k < num; k++)
            {
                random.Int(0, 1);
            }

            var num2 = CalculateMountainRangeFactor(i * 16, j * 16);
            var num3 = (int)(5f + 3f * num2 * SimplexNoise.OctavedNoise(i, j, 0.33f, 1, 1f, 1f));
            for (var l = 0; l < num3; l++)
            {
                var x2 = i * 16 + random.Int(0, 15);
                var y2 = random.Int(5, 200);
                var z = j * 16 + random.Int(0, 15);
                _coalBrushes[random.Int(0, _coalBrushes.Count - 1)].PaintFastSelective(chunk, x2, y2, z, 3);
            }

            var num4 = (int)(6f + 2f * num2 * SimplexNoise.OctavedNoise(i + 1211, j + 396, 0.33f, 1, 1f, 1f));
            for (var m = 0; m < num4; m++)
            {
                var x3 = i * 16 + random.Int(0, 15);
                var y3 = random.Int(20, 65);
                var z2 = j * 16 + random.Int(0, 15);
                _copperBrushes[random.Int(0, _copperBrushes.Count - 1)].PaintFastSelective(chunk, x3, y3, z2, 3);
            }

            var num5 = (int)(5f + 2f * num2 * SimplexNoise.OctavedNoise(i + 713, j + 211, 0.33f, 1, 1f, 1f));
            for (var n = 0; n < num5; n++)
            {
                var x4 = i * 16 + random.Int(0, 15);
                var y4 = random.Int(2, 40);
                var z3 = j * 16 + random.Int(0, 15);
                _ironBrushes[random.Int(0, _ironBrushes.Count - 1)].PaintFastSelective(chunk, x4, y4, z3, 67);
            }

            var num6 = (int)(3f + 3f * num2 * SimplexNoise.OctavedNoise(i + 915, j + 272, 0.33f, 1, 1f, 1f));
            for (var num7 = 0; num7 < num6; num7++)
            {
                var x5 = i * 16 + random.Int(0, 15);
                var y5 = random.Int(50, 90);
                var z4 = j * 16 + random.Int(0, 15);
                _saltpeterBrushes[random.Int(0, _saltpeterBrushes.Count - 1)]
                    .PaintFastSelective(chunk, x5, y5, z4, 4);
            }

            var num8 = (int)(3f + 2f * num2 * SimplexNoise.OctavedNoise(i + 711, j + 1194, 0.33f, 1, 1f, 1f));
            for (var num9 = 0; num9 < num8; num9++)
            {
                var x6 = i * 16 + random.Int(0, 15);
                var y6 = random.Int(2, 40);
                var z5 = j * 16 + random.Int(0, 15);
                _sulphurBrushes[random.Int(0, _sulphurBrushes.Count - 1)].PaintFastSelective(chunk, x6, y6, z5, 67);
            }

            var num10 = (int)(0.5f + 2f * num2 * SimplexNoise.OctavedNoise(i + 432, j + 907, 0.33f, 1, 1f, 1f));
            for (var num11 = 0; num11 < num10; num11++)
            {
                var x7 = i * 16 + random.Int(0, 15);
                var y7 = random.Int(2, 15);
                var z6 = j * 16 + random.Int(0, 15);
                _diamondBrushes[random.Int(0, _diamondBrushes.Count - 1)].PaintFastSelective(chunk, x7, y7, z6, 67);
            }

            var num12 = (int)(3f + 2f * num2 * SimplexNoise.OctavedNoise(i + 799, j + 131, 0.33f, 1, 1f, 1f));
            for (var num13 = 0; num13 < num12; num13++)
            {
                var x8 = i * 16 + random.Int(0, 15);
                var y8 = random.Int(2, 50);
                var z7 = j * 16 + random.Int(0, 15);
                _germaniumBrushes[random.Int(0, _germaniumBrushes.Count - 1)]
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
            var num3 = random.Int(0, 10);
            for (var k = 0; k < num3; k++)
            {
                random.Int(0, 1);
            }

            var num4 = CalculateMountainRangeFactor(num * 16, num2 * 16);
            for (var l = 0; l < 3; l++)
            {
                var x = num * 16 + random.Int(0, 15);
                var y = random.Int(50, 100);
                var z = num2 * 16 + random.Int(0, 15);
                _dirtPocketBrushes[random.Int(0, _dirtPocketBrushes.Count - 1)].PaintFastSelective(chunk, x, y, z, 3);
            }

            for (var m = 0; m < 10; m++)
            {
                var x2 = num * 16 + random.Int(0, 15);
                var y2 = random.Int(20, 80);
                var z2 = num2 * 16 + random.Int(0, 15);
                _gravelPocketBrushes[random.Int(0, _gravelPocketBrushes.Count - 1)]
                    .PaintFastSelective(chunk, x2, y2, z2, 3);
            }

            for (var n = 0; n < 2; n++)
            {
                var x3 = num * 16 + random.Int(0, 15);
                var y3 = random.Int(20, 120);
                var z3 = num2 * 16 + random.Int(0, 15);
                _limestonePocketBrushes[random.Int(0, _limestonePocketBrushes.Count - 1)]
                    .PaintFastSelective(chunk, x3, y3, z3, 3);
            }

            for (var num5 = 0; num5 < 1; num5++)
            {
                var x4 = num * 16 + random.Int(0, 15);
                var y4 = random.Int(50, 70);
                var z4 = num2 * 16 + random.Int(0, 15);
                _clayPocketBrushes[random.Int(0, _clayPocketBrushes.Count - 1)]
                    .PaintFastSelective(chunk, x4, y4, z4, 3);
            }

            for (var num6 = 0; num6 < 6; num6++)
            {
                var x5 = num * 16 + random.Int(0, 15);
                var y5 = random.Int(40, 80);
                var z5 = num2 * 16 + random.Int(0, 15);
                _sandPocketBrushes[random.Int(0, _sandPocketBrushes.Count - 1)]
                    .PaintFastSelective(chunk, x5, y5, z5, 4);
            }

            for (var num7 = 0; num7 < 4; num7++)
            {
                var x6 = num * 16 + random.Int(0, 15);
                var y6 = random.Int(40, 60);
                var z6 = num2 * 16 + random.Int(0, 15);
                _basaltPocketBrushes[random.Int(0, _basaltPocketBrushes.Count - 1)]
                    .PaintFastSelective(chunk, x6, y6, z6, 4);
            }

            for (var num8 = 0; num8 < 3; num8++)
            {
                var x7 = num * 16 + random.Int(0, 15);
                var y7 = random.Int(20, 40);
                var z7 = num2 * 16 + random.Int(0, 15);
                _basaltPocketBrushes[random.Int(0, _basaltPocketBrushes.Count - 1)]
                    .PaintFastSelective(chunk, x7, y7, z7, 3);
            }

            for (var num9 = 0; num9 < 6; num9++)
            {
                var x8 = num * 16 + random.Int(0, 15);
                var y8 = random.Int(4, 50);
                var z8 = num2 * 16 + random.Int(0, 15);
                _granitePocketBrushes[random.Int(0, _granitePocketBrushes.Count - 1)]
                    .PaintFastSelective(chunk, x8, y8, z8, 67);
            }

            if (random.Bool(0.02f + 0.01f * num4))
            {
                var num10 = num * 16;
                var num11 = random.Int(40, 60);
                var num12 = num2 * 16;
                var num13 = random.Int(1, 3);
                for (var num14 = 0; num14 < num13; num14++)
                {
                    var vector = random.Vector2(7f);
                    var num15 = 8 + (int)MathUtils.Round(vector.X);
                    var num16 = 0;
                    var num17 = 8 + (int)MathUtils.Round(vector.Y);
                    _waterPocketBrushes[random.Int(0, _waterPocketBrushes.Count - 1)]
                        .PaintFast(chunk, num10 + num15, num11 + num16, num12 + num17);
                }
            }

            if (!random.Bool(0.06f + 0.05f * num4))
            {
                continue;
            }

            var num18 = num * 16;
            var num19 = random.Int(15, 42);
            var num20 = num2 * 16;
            var num21 = random.Int(1, 2);
            for (var num22 = 0; num22 < num21; num22++)
            {
                var vector2 = random.Vector2(7f);
                var num23 = 8 + (int)MathUtils.Round(vector2.X);
                var num24 = random.Int(0, 1);
                var num25 = 8 + (int)MathUtils.Round(vector2.Y);
                _magmaPocketBrushes[random.Int(0, _magmaPocketBrushes.Count - 1)]
                    .PaintFast(chunk, num18 + num23, num19 + num24, num20 + num25);
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
            var num = i * 16 + random.Int(0, 15);
            var num2 = j * 16 + random.Int(0, 15);
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
                    Length = random.Int(80, 240)
                });
            }

            var num6 = i * 16 + 8;
            var num7 = j * 16 + 8;
            var num8 = 0;
            while (num8 < list.Count)
            {
                var cavePoint = list[num8];
                var list2 = _caveBrushesByType[cavePoint.BrushType];
                list2[random.Int(0, list2.Count - 1)].PaintFastAvoidWater(chunk, Terrain.ToCell(cavePoint.Position.X),
                    Terrain.ToCell(cavePoint.Position.Y), Terrain.ToCell(cavePoint.Position.Z));
                cavePoint.Position += 2f * cavePoint.Direction;
                cavePoint.StepsTaken += 2;
                var num9 = cavePoint.Position.X - num6;
                var num10 = cavePoint.Position.Z - num7;
                if (random.Bool(0.5f))
                {
                    var vector = Vector3.Normalize(random.Vector3(1f));
                    if ((num9 < -25.5f && vector.X < 0f) || (num9 > 25.5f && vector.X > 0f))
                    {
                        vector.X = 0f - vector.X;
                    }

                    if ((num10 < -25.5f && vector.Z < 0f) || (num10 > 25.5f && vector.Z > 0f))
                    {
                        vector.Z = 0f - vector.Z;
                    }

                    if ((cavePoint.Direction.Y < -0.5f && vector.Y < -10f) ||
                        (cavePoint.Direction.Y > 0.1f && vector.Y > 0f))
                    {
                        vector.Y = 0f - vector.Y;
                    }

                    cavePoint.Direction = Vector3.Normalize(cavePoint.Direction + 0.5f * vector);
                }

                if (cavePoint.StepsTaken > 20 && random.Bool(0.06f))
                {
                    cavePoint.Direction = Vector3.Normalize(random.Vector3(1f) * new Vector3(1f, 0.33f, 1f));
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
                        (int)(MathUtils.Pow(random.Float(0f, 0.999f), 7f) * _caveBrushesByType.Count);
                }

                if (random.Bool(0.06f) && list.Count < 12 && cavePoint.StepsTaken > 20 && cavePoint.Position.Y < 58f)
                {
                    list.Add(new CavePoint
                    {
                        Position = cavePoint.Position,
                        Direction = Vector3.Normalize(random.Vector3(1f, 1f) * new Vector3(1f, 0.33f, 1f)),
                        BrushType = (int)(MathUtils.Pow(random.Float(0f, 0.999f), 7f) * _caveBrushesByType.Count),
                        Length = random.Int(40, 180)
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

    public void GenerateLogs(TerrainChunk chunk)
    {
        if (!_tgExtras)
        {
            return;
        }

        var x = chunk.Origin.X;
        var num = x + 16;
        var y = chunk.Origin.Y;
        var num2 = y + 16;
        var x2 = chunk.Coords.X;
        var y2 = chunk.Coords.Y;
        for (var i = x2; i <= x2; i++)
        for (var j = y2; j <= y2; j++)
        {
            var random = new Random(_seed + i + 3943 * j);
            var humidity = CalculateHumidity(i * 16, j * 16);
            var num3 = CalculateTemperature(i * 16, j * 16);
            var num4 = MathUtils.Saturate((SimplexNoise.OctavedNoise(i, j, 0.1f, 2, 2f, 0.5f) - 0.25f) / 0.2f +
                                          (random.Bool(0.25f) ? 0.5f : 0f));
            var num5 = 0;
            if (num4 > 0.9f)
            {
                num5 = random.Int(1, 2);
            }
            else if (num4 > 0.5f)
            {
                num5 = random.Int(0, 1);
            }

            var num6 = 0;
            for (var k = 0; k < 12; k++)
            {
                if (num6 >= num5)
                {
                    break;
                }

                var num7 = i * 16 + random.Int(0, 15);
                var num8 = j * 16 + random.Int(0, 15);
                var num9 = _terrain.CalculateTopmostCellHeight(num7, num8);
                if (num9 < 66)
                {
                    continue;
                }

                var cellContentsFast = _terrain.GetCellContentsFast(num7, num9, num8);
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
                    if (num11 < x + 1 ||
                        num11 >= num - 1 ||
                        num12 < y + 1 ||
                        num12 >= num2 - 1 ||
                        BlocksManager.Blocks[_terrain.GetCellContentsFast(num11, num9, num12)].Collidable)
                    {
                        flag = false;
                        break;
                    }

                    if (!BlocksManager.Blocks[_terrain.GetCellContentsFast(num11, num9 - 1, num12)].Collidable)
                    {
                        continue;
                    }

                    if (l <= MathUtils.Max(num10 / 2, 0))
                    {
                        flag2 = true;
                    }

                    if (l >= MathUtils.Min(num10 / 2 + 1, num10 - 1))
                    {
                        flag3 = true;
                    }
                }

                if (!(flag && flag2 && flag3))
                {
                    continue;
                }

                var point2 = point.X != 0 ? new Point3(0, 0, 1) : new Point3(1, 0, 0);
                var treeType = PlantsManager.GenerateRandomTreeType(random,
                    num3 + SubsystemWeather.GetTemperatureAdjustmentAtHeight(num9), humidity, num9, 2f);
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
                        _terrain.SetCellValueFast(num13, num9, num14, treeTrunkValue);
                        if (m <= num10 / 2)
                        {
                            continue;
                        }

                        if (random.Bool(0.5f) && !BlocksManager
                                .Blocks[_terrain.GetCellContentsFast(num13 + point2.X, num9, num14 + point2.Z)]
                                .Collidable)
                        {
                            _terrain.SetCellValueFast(num13 + point2.X, num9, num14 + point2.Z, treeLeavesValue);
                        }

                        if (random.Bool(0.05f) && !BlocksManager
                                .Blocks[_terrain.GetCellContentsFast(num13 + point2.X, num9, num14 + point2.Z)]
                                .Collidable)
                        {
                            _terrain.SetCellValueFast(num13 + point2.X, num9, num14 + point2.Z, treeTrunkValue);
                        }

                        if (random.Bool(0.5f) && !BlocksManager
                                .Blocks[_terrain.GetCellContentsFast(num13 - point2.X, num9, num14 - point2.Z)]
                                .Collidable)
                        {
                            _terrain.SetCellValueFast(num13 - point2.X, num9, num14 - point2.Z, treeLeavesValue);
                        }

                        if (random.Bool(0.05f) && !BlocksManager
                                .Blocks[_terrain.GetCellContentsFast(num13 - point2.X, num9, num14 - point2.Z)]
                                .Collidable)
                        {
                            _terrain.SetCellValueFast(num13 - point2.X, num9, num14 - point2.Z, treeTrunkValue);
                        }

                        if (random.Bool(0.5f) && !BlocksManager
                                .Blocks[_terrain.GetCellContentsFast(num13, num9 + 1, num14)]
                                .Collidable)
                        {
                            _terrain.SetCellValueFast(num13, num9 + 1, num14, treeLeavesValue);
                        }
                    }
                }

                num6++;
            }
        }
    }

    public void GenerateTrees(TerrainChunk chunk)
    {
        if (!_tgExtras)
        {
            return;
        }

        var x = chunk.Coords.X;
        var y = chunk.Coords.Y;

        var sourceCoords = new Point2[9];
        var sourceIndex = 0;
        for (var i = x - 1; i < x + 2; i++)
        for (var j = y - 1; j < y + 2; j++)
        {
            sourceCoords[sourceIndex++] = new Point2(i, j);
        }

        WarmCleanTreeBrushCache(sourceCoords);

        foreach (var coords in sourceCoords)
        {
            foreach (var brushPaint in GetCleanTreeBrush(coords))
            {
                brushPaint.Brush.PaintFast(chunk, brushPaint.Position.X, brushPaint.Position.Y, brushPaint.Position.Z);
            }
        }
    }

    private void WarmCleanTreeBrushCache(Point2[] sourceCoords)
    {
        var missingCoords = sourceCoords
            .Where(coords => !_cleanTreeBrushCache.ContainsKey((_seed, coords)))
            .ToArray();
        if (missingCoords.Length == 0)
        {
            return;
        }

        Parallel.ForEach(missingCoords, new ParallelOptions
        {
            MaxDegreeOfParallelism = SeedTerrainGenerationPolicy.GetParallelism(Environment.ProcessorCount)
        }, coords => _ = GetCleanTreeBrush(coords));
    }

    private BrushPaint[] GetCleanTreeBrush(Point2 coords)
    {
        var entry = _cleanTreeBrushCache.GetOrAdd((_seed, coords), static (key, generator) =>
            generator.CreateCleanTreeBrushEntry(key.Coords), this);
        entry.LastAccessTicks = Environment.TickCount64;
        MaybeCleanupCleanTreeBrushCache();
        return entry.BrushPaints;
    }

    private CleanTreeBrushCacheEntry CreateCleanTreeBrushEntry(Point2 coords)
    {
        var sourceChunk = CreateTreeBrushSourceChunk(coords);
        var brushPaints = GenerateCleanTreeBrush(sourceChunk);
        var basis = new SeedGeneratedChunkBasis(sourceChunk.Cells, sourceChunk.Shafts);
        sourceChunk.Dispose();
        return new CleanTreeBrushCacheEntry(brushPaints, Environment.TickCount64, basis);
    }

    private void RegisterCleanTreeBrush(TerrainChunk chunk)
    {
        _cleanTreeBrushCache.GetOrAdd((_seed, chunk.Coords), static (_, state) =>
            new CleanTreeBrushCacheEntry(state.Generator.GenerateCleanTreeBrush(state.Chunk),
                Environment.TickCount64, null), (Generator: this, Chunk: chunk));
    }

    public bool TryTakeSeedGeneratedChunkBasis(TerrainChunk chunk)
    {
        return _cleanTreeBrushCache.TryGetValue((_seed, chunk.Coords), out var entry) &&
               entry.Basis?.TryMoveTo(chunk) == true;
    }

    private TerrainChunk CreateTreeBrushSourceChunk(Point2 coords)
    {
        var chunk = new TerrainChunk(_terrain, coords.X, coords.Y);
        GenerateChunkContentsPass1(chunk);
        GenerateChunkContentsPass2(chunk);
        GenerateChunkContentsPass3(chunk);
        return chunk;
    }

    private BrushPaint[] GenerateCleanTreeBrush(TerrainChunk chunk)
    {
        var result = new List<BrushPaint>();
        var x = chunk.Coords.X;
        var y = chunk.Coords.Y;
        for (var i = x; i <= x; i++)
        for (var j = y; j <= y; j++)
        {
            var random = new Random(_seed + i + 3943 * j);
            var humidity = CalculateHumidity(i * 16, j * 16);
            var temperature = CalculateTemperature(i * 16, j * 16);
            var forestDensity = MathUtils.Saturate((SimplexNoise.OctavedNoise(i, j, 0.1f, 2, 2f, 0.5f) - 0.25f) / 0.2f +
                                                   (random.Bool(0.25f) ? 0.5f : 0f));
            var treeLimit = (int)(5f * forestDensity);
            var treeCount = 0;
            for (var k = 0; k < 32; k++)
            {
                if (treeCount >= treeLimit)
                {
                    break;
                }

                var randomX = i * 16 + random.Int(2, 13);
                var randomZ = j * 16 + random.Int(2, 13);
                var localX = randomX & 0xF;
                var localZ = randomZ & 0xF;
                var heightLimit = chunk.CalculateTopmostCellHeight(localX, localZ);
                if (heightLimit < 66)
                {
                    continue;
                }

                var cellContentsFast = chunk.GetCellContentsFast(localX, heightLimit, localZ);
                if (cellContentsFast != 2 && cellContentsFast != 8)
                {
                    continue;
                }

                heightLimit++;

                if (BlocksManager.Blocks[chunk.GetCellContentsFast(localX + 1, heightLimit, localZ)].Collidable ||
                    BlocksManager.Blocks[chunk.GetCellContentsFast(localX - 1, heightLimit, localZ)].Collidable ||
                    BlocksManager.Blocks[chunk.GetCellContentsFast(localX, heightLimit, localZ + 1)].Collidable ||
                    BlocksManager.Blocks[chunk.GetCellContentsFast(localX, heightLimit, localZ - 1)].Collidable)
                {
                    continue;
                }

                var treeType = PlantsManager.GenerateRandomTreeType(random,
                    temperature + SubsystemWeather.GetTemperatureAdjustmentAtHeight(heightLimit), humidity,
                    heightLimit);
                if (treeType.HasValue)
                {
                    var treeBrushes = PlantsManager.GetTreeBrushes(treeType.Value);
                    var treeBrush = treeBrushes[random.Int(treeBrushes.Count)];
                    result.Add(new BrushPaint
                        {
                            Position = new Point3(randomX, heightLimit, randomZ),
                            Brush = treeBrush
                        }
                    );
                }

                treeCount++;
            }
        }

        return result.ToArray();
    }

    private void MaybeCleanupCleanTreeBrushCache()
    {
        if ((Interlocked.Increment(ref _cleanTreeBrushRequestCount) & (_cleanTreeBrushCacheCleanupInterval - 1)) != 0)
        {
            return;
        }

        var now = Environment.TickCount64;
        var previousCleanupTicks = Interlocked.Read(ref _cleanTreeBrushCleanupTicks);
        if (now - previousCleanupTicks < 5000)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _cleanTreeBrushCleanupTicks, now, previousCleanupTicks) !=
            previousCleanupTicks)
        {
            return;
        }

        foreach (var (key, entry) in _cleanTreeBrushCache)
        {
            if (now - entry.LastAccessTicks > _cleanTreeBrushCacheMaxAgeMs)
            {
                _cleanTreeBrushCache.TryRemove(key, out _);
            }
        }

        var overflow = _cleanTreeBrushCache.Count - _cleanTreeBrushCacheMaxEntries;
        if (overflow <= 0)
        {
            return;
        }

        var oldestEntries = _cleanTreeBrushCache
            .OrderBy(pair => pair.Value.LastAccessTicks)
            .Take(overflow)
            .ToArray();

        foreach (var (key, _) in oldestEntries)
        {
            _cleanTreeBrushCache.TryRemove(key, out _);
        }
    }

    private sealed class CleanTreeBrushCacheEntry(
        BrushPaint[] brushPaints,
        long lastAccessTicks,
        SeedGeneratedChunkBasis? basis)
    {
        public readonly BrushPaint[] BrushPaints = brushPaints;

        public readonly SeedGeneratedChunkBasis? Basis = basis;

        public long LastAccessTicks = lastAccessTicks;
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

        var random = new Random(_seed + chunk.Coords.X + 3943 * chunk.Coords.Y);
        for (var i = 0; i < 16; i++)
        for (var j = 0; j < 16; j++)
        for (var num = 254; num >= 0; num--)
        {
            var cellValueFast = chunk.GetCellValueFast(i, num, j);
            var num2 = Terrain.ExtractContents(cellValueFast);
            if (num2 == 0)
            {
                continue;
            }

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
                    var face = random.Int(0, 5);
                    var point = CellFace.FaceToPoint3(face);
                    if (i + point.X < 0 || i + point.X >= 16 || num4 + point.Y < 0 || num4 + point.Y >= 254 ||
                        j + point.Z < 0 || j + point.Z >= 16)
                    {
                        continue;
                    }

                    var cellValueFast = chunk.GetCellValueFast(i + point.X, num4 + point.Y, j + point.Z);
                    if (!_subsystemBottomSuckerBlockBehavior.IsSupport(cellValueFast, CellFace.OppositeFace(face)))
                    {
                        continue;
                    }

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

                    var num8 = random.Float(0f, 1f);
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

                    if (num5 == 0)
                    {
                        continue;
                    }

                    var face2 = random.Int(0, 3);
                    var data = BottomSuckerBlock.SetFace(BottomSuckerBlock.SetSubvariant(0, face2),
                        CellFace.OppositeFace(face));
                    var value = Terrain.MakeBlockValue(num5, 0, data);
                    chunk.SetCellValueFast(i, num4, j, value);
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

        var num = random.Int(0, MathUtils.Max(1, 1));
        for (var i = 0; i < num; i++)
        {
            var num2 = random.Int(3, 12);
            var num3 = random.Int(3, 12);
            var humidityFast = chunk.GetHumidityFast(num2, num3);
            var temperatureFast = chunk.GetTemperatureFast(num2, num3);
            if (humidityFast >= 6 || temperatureFast <= 8)
            {
                continue;
            }

            for (var j = 0; j < 8; j++)
            {
                var num4 = num2 + random.Int(-2, 2);
                var num5 = num3 + random.Int(-2, 2);
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

        var num = random.Int(0, MathUtils.Max(1, 1));
        for (var i = 0; i < num; i++)
        {
            var num2 = random.Int(1, 14);
            var num3 = random.Int(1, 14);
            var humidityFast = chunk.GetHumidityFast(num2, num3);
            var temperatureFast = chunk.GetTemperatureFast(num2, num3);
            if (humidityFast < 10 || temperatureFast <= 6)
            {
                continue;
            }

            for (var j = 0; j < 5; j++)
            {
                var x2 = num2 + random.Int(-1, 1);
                var z = num3 + random.Int(-1, 1);
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
            random.Seed(_seed + x + num2 + 850 * (y + num3));
            if (!random.Bool(0.2f))
            {
                continue;
            }

            num = MathUtils.Max(num, 0.025f);
            if (i == 4)
            {
                num = MathUtils.Max(num, 0.1f);
            }
        }

        if (num == 0f)
        {
            return;
        }

        random.Seed(_seed + x + 850 * y);
        var num4 = random.Int(0, MathUtils.Max((int)(256f * num), 1));
        for (var j = 0; j < num4; j++)
        {
            var num5 = random.Int(2, 13);
            var num6 = random.Int(2, 13);
            var num7 = num5 + chunk.Origin.X;
            var num8 = num6 + chunk.Origin.Y;
            var num9 = random.Int(10, 26);
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
                var x2 = num5 + random.Int(-2, 2);
                var z = num6 + random.Int(-2, 2);
                var num11 = 0;
                for (var num12 = 254; num12 >= 0; num12--)
                {
                    var num13 = Terrain.ExtractContents(chunk.GetCellValueFast(x2, num12, z));
                    var block = BlocksManager.Blocks[num13];
                    if (num13 == 0)
                    {
                        continue;
                    }

                    if (block is not WaterBlock)
                    {
                        if (num13 is 2 or 7 or 72 && num11 >= 2)
                        {
                            var num14 = flag ? random.Int(num11 - 2, num11 - 1) : random.Int(num11 - 1, num11);
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
            var num = random.Int(1, 14);
            var num2 = random.Int(1, 14);
            var num3 = chunk.Origin.X + num;
            var num4 = chunk.Origin.Y + num2;
            var flag = CalculateOceanShoreDistance(num3, num4) < 10f;
            var num5 = random.Int(1, 3);
            for (var j = 0; j < num5; j++)
            {
                var x2 = num + random.Int(-1, 1);
                var z = num2 + random.Int(-1, 1);
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
        var num = random.Int(0, MathUtils.Max(12, 1));
        for (var i = 0; i < num; i++)
        {
            var num2 = random.Int(4, 11);
            var num3 = random.Int(4, 11);
            var humidityFast = chunk.GetHumidityFast(num2, num3);
            var temperatureFast = chunk.GetTemperatureFast(num2, num3);
            if (humidityFast < 10 || temperatureFast < 10)
            {
                continue;
            }

            var num4 = chunk.CalculateTopmostCellHeight(num2, num3);
            for (var j = 0; j < 100; j++)
            {
                var num5 = num2 + random.Int(-3, 3);
                var num6 = MathUtils.Clamp(num4 + random.Int(-12, 1), 1, 255);
                var num7 = num3 + random.Int(-3, 3);
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
                        var num8 = random.Int(0, 3);
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
        var random = new Random(_seed + x + 2113 * y);
        if (!random.Bool(0.15f) || !(CalculateOceanShoreDistance(chunk.Origin.X, chunk.Origin.Y) > 50f))
        {
            return;
        }

        var num = random.Int(0, MathUtils.Max(2, 1));
        for (var i = 0; i < num; i++)
        {
            var num2 = random.Int(2, 5);
            var num3 = random.Int(2, 5);
            var num4 = random.Int(1, 16 - num2 - 2);
            var num5 = random.Int(1, 16 - num3 - 2);
            var flag = random.Float(0f, 1f) < 0.5f;
            var num6 = random.Int(3, 5);
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
                            goto end_IL_019b;
                        }

                        num7 = num9;
                        if (chunk.GetCellContentsFast(num8, num9, j) != 8)
                        {
                            goto end_IL_019b;
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
                    if (!flag)
                    {
                        continue;
                    }

                    var data = SpikedPlankBlock.SetSpikesState(0, random.Float(0f, 1f) < 0.33f);
                    chunk.SetCellValueFast(k, num7.Value - num6 + 1, l, Terrain.MakeBlockValue(86, 0, data));
                }

                break;
                end_IL_019b:
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
        if (!(random.Float(0f, 1f) < 0.033f) ||
            !(CalculateOceanShoreDistance(chunk.Origin.X, chunk.Origin.Y) > 10f))
        {
            return;
        }

        var num = random.Int(0, MathUtils.Max(1, 1));
        for (var i = 0; i < num; i++)
        {
            var num2 = random.Int(6, 9);
            var num3 = random.Int(6, 9);
            var num4 = random.Bool(0.2f) ? random.Int(6, 20) : random.Int(1, 5);
            var flag = random.Bool(0.5f);
            for (var j = 0; j < num4; j++)
            {
                var num5 = num2 + random.Int(-4, 4);
                var num6 = num3 + random.Int(-4, 4);
                var num7 = chunk.CalculateTopmostCellHeight(num5, num6);
                if (num7 is < 10 or > 246)
                {
                    continue;
                }

                var num8 = random.Int(0, 3);
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
                        else if (num14 is 8 or 2 or 7 or 3 or 4)
                        {
                            continue;
                        }

                        goto IL_06ac;
                    }

                    var num15 = random.Int(0, 7);
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
                        else if (random.Float(0f, 1f) < 0.5f)
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
                    var num17 = random.Float(0f, 1f);
                    var num18 = random.Float(0f, 1f);
                    var num19 = random.Int(-1, 0);
                    var num20 = random.Int(1, 2);
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
                            if (num22 is 0 or 1)
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

    public void GenerateCairns(TerrainChunk chunk)
    {
        var num = 190;
        var point = default(Point2);
        for (var i = 0; i < 16; i++)
        for (var j = 0; j < 16; j++)
        {
            if (Terrain.ExtractContents(chunk.GetCellValueFast(i, num, j)) == 0)
            {
                continue;
            }

            for (var k = num + 1; k < 256; k++)
            {
                if (Terrain.ExtractContents(chunk.GetCellValueFast(i, k, j)) != 0)
                {
                    continue;
                }

                num = k;
                point = new Point2(i, j);
                break;
            }
        }

        if (num is < 190 or > 255 || point.X is < 1 or >= 15 || point.Y is < 1 or >= 15)
        {
            return;
        }

        var data = MathUtils.Clamp((int)(4f * MathUtils.LinearStep(190f, 256f, num)), 0, 3);
        chunk.SetCellValueFast(point.X, num, point.Y, Terrain.MakeBlockValue(258, 0, data));
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
                if (num4 == 0)
                {
                    continue;
                }

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
                            if (num3 - k <= 0)
                            {
                                continue;
                            }

                            if (!(BlocksManager.Blocks[
                                    chunk.GetCellContentsFast(i, num3 - k, j)] is WaterBlock))
                            {
                                break;
                            }

                            chunk.SetCellValueFast(i, num3 - k, j, 62);
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
            var num = random.Int(4, 12);
            for (var j = 0; j < num; j++)
            {
                var vector = 0.5f * Vector3.Normalize(new Vector3(random.Float(-1f, 1f), random.Float(-1f, 1f),
                    random.Float(-1f, 1f)));
                var num2 = random.Int(3, 8);
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
            var num3 = random.Int(3, 7);
            for (var m = 0; m < num3; m++)
            {
                var vector2 = 0.5f * Vector3.Normalize(new Vector3(random.Float(-1f, 1f), random.Float(-1f, 1f),
                    random.Float(-1f, 1f)));
                var num4 = random.Int(3, 6);
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
            var num6 = random.Int(4, 10);
            for (var num7 = 0; num7 < num6; num7++)
            {
                var vector3 = 0.5f * Vector3.Normalize(new Vector3(random.Float(-1f, 1f), random.Float(-2f, 2f),
                    random.Float(-1f, 1f)));
                var num8 = random.Int(3, 6);
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
            var num11 = random.Int(8, 16);
            for (var num12 = 0; num12 < num11; num12++)
            {
                var vector4 = 0.5f * Vector3.Normalize(new Vector3(random.Float(-1f, 1f), random.Float(-0.25f, 0.25f),
                    random.Float(-1f, 1f)));
                var num13 = random.Int(4, 8);
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
            var num16 = random.Int(4, 10);
            for (var num17 = 0; num17 < num16; num17++)
            {
                var vector5 = 0.5f * Vector3.Normalize(new Vector3(random.Float(-1f, 1f), random.Float(-1f, 1f),
                    random.Float(-1f, 1f)));
                var num18 = random.Int(3, 6);
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
            var num21 = random.Int(2, 6);
            for (var num22 = 0; num22 < num21; num22++)
            {
                var vector6 = 0.5f * Vector3.Normalize(new Vector3(random.Float(-1f, 1f), random.Float(-1f, 1f),
                    random.Float(-1f, 1f)));
                var num23 = random.Int(3, 6);
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
            var num26 = random.Int(4, 10);
            for (var num27 = 0; num27 < num26; num27++)
            {
                var vector7 = 0.5f * Vector3.Normalize(new Vector3(random.Float(-1f, 1f), random.Float(-1f, 1f),
                    random.Float(-1f, 1f)));
                var num28 = random.Int(3, 6);
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
            var num31 = random.Int(16, 32);
            for (var num32 = 0; num32 < num31; num32++)
            {
                var vector8 = 0.5f * Vector3.Normalize(new Vector3(random.Float(-1f, 1f), random.Float(-0.75f, 0.75f),
                    random.Float(-1f, 1f)));
                var num33 = random.Int(6, 12);
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
            var num36 = random.Int(16, 32);
            for (var num37 = 0; num37 < num36; num37++)
            {
                var vector9 = 0.5f * Vector3.Normalize(new Vector3(random.Float(-1f, 1f), random.Float(-0.75f, 0.75f),
                    random.Float(-1f, 1f)));
                var num38 = random.Int(6, 12);
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
            var num41 = random.Int(16, 32);
            for (var num42 = 0; num42 < num41; num42++)
            {
                var vector10 = 0.5f * Vector3.Normalize(new Vector3(random.Float(-1f, 1f), random.Float(-0.75f, 0.75f),
                    random.Float(-1f, 1f)));
                var num43 = random.Int(6, 12);
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
            var num46 = random.Int(16, 32);
            for (var num47 = 0; num47 < num46; num47++)
            {
                var vector11 = 0.5f * Vector3.Normalize(new Vector3(random.Float(-1f, 1f), random.Float(-0.1f, 0.1f),
                    random.Float(-1f, 1f)));
                var num48 = random.Int(6, 12);
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
            var num51 = random.Int(16, 32);
            for (var num52 = 0; num52 < num51; num52++)
            {
                var vector12 = 0.5f * Vector3.Normalize(new Vector3(random.Float(-1f, 1f), random.Float(-0.75f, 0.75f),
                    random.Float(-1f, 1f)));
                var num53 = random.Int(6, 12);
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
            var num56 = random.Int(16, 32);
            for (var num57 = 0; num57 < num56; num57++)
            {
                var vector13 = 0.5f * Vector3.Normalize(new Vector3(random.Float(-1f, 1f), random.Float(-0.75f, 0.75f),
                    random.Float(-1f, 1f)));
                var num58 = random.Int(6, 12);
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
            var num61 = random.Int(16, 32);
            for (var num62 = 0; num62 < num61; num62++)
            {
                var vector14 = 0.5f * Vector3.Normalize(new Vector3(random.Float(-1f, 1f), random.Float(-1f, 1f),
                    random.Float(-1f, 1f)));
                var num63 = random.Int(5, 10);
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

        var array = new int[3] { 4, 6, 8 };
        for (var num65 = 0; num65 < 4 * array.Length; num65++)
        {
            var terrainBrush15 = new TerrainBrush();
            var num66 = array[num65 / 4];
            var num67 = num65 % 2 + 1;
            var num68 = num65 % 4 == 2 ? 0.5f : 1f;
            var num69 = num65 % 4 == 1 ? num66 * num66 : 2 * num66 * num66;
            for (var num70 = 0; num70 < num69; num70++)
            {
                var vector15 = random.Vector2(0f, num66);
                var num71 = vector15.Length();
                var num72 = random.Int(3, 4);
                var sizeY = 1 + (int)MathUtils.Lerp(MathUtils.Max(num66 / 3, 2.5f) * num68, 0f, num71 / num66) +
                            random.Int(0, 1);
                terrainBrush15.AddBox((int)MathUtils.Floor(vector15.X), 0, (int)MathUtils.Floor(vector15.Y), num72,
                    sizeY, num72, 0);
                terrainBrush15.AddBox((int)MathUtils.Floor(vector15.X), -num67, (int)MathUtils.Floor(vector15.Y), num72,
                    num67, num72, 18);
            }

            terrainBrush15.Compile();
            _waterPocketBrushes.Add(terrainBrush15);
        }

        var array2 = new int[4] { 8, 12, 14, 16 };
        for (var num73 = 0; num73 < 4 * array2.Length; num73++)
        {
            var terrainBrush16 = new TerrainBrush();
            var num74 = array2[num73 / 4];
            var num75 = num74 + 2;
            var num76 = num73 % 4 == 2 ? 0.5f : 1f;
            var num77 = num73 % 4 == 1 ? num74 * num74 : 2 * num74 * num74;
            for (var num78 = 0; num78 < num77; num78++)
            {
                var vector16 = random.Vector2(0f, num74);
                var num79 = vector16.Length();
                var num80 = random.Int(3, 4);
                var sizeY2 = 1 + (int)MathUtils.Lerp(MathUtils.Max(num74 / 3, 2.5f) * num76, 0f, num79 / num74) +
                             random.Int(0, 1);
                var num81 = 1 + (int)MathUtils.Lerp(num75, 0f, num79 / num74) + random.Int(0, 1);
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
                    var num86 = random.Int(2, max);
                    var num87 = random.Int(8, max2) - 2 * num86;
                    var vector17 = 0.5f * new Vector3(random.Float(-1f, 1f), random.Float(0f, 1f),
                        random.Float(-1f, 1f));
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

        private readonly int _sizeXy;

        private readonly int _sizeY;

        private readonly int _sizeZ;

        public Grid3D(int sizeX, int sizeY, int sizeZ)
        {
            _sizeX = sizeX;
            _sizeY = sizeY;
            _sizeZ = sizeZ;
            _sizeXy = _sizeX * _sizeY;
            _data = new float[_sizeX * _sizeY * _sizeZ];
        }

        public int SizeX => _sizeX;

        public int SizeY => _sizeY;

        public int SizeZ => _sizeZ;

        public void Get8(int x, int y, int z, out float v111, out float v211, out float v121, out float v221,
            out float v112, out float v212, out float v122, out float v222)
        {
            var num = x + y * _sizeX + z * _sizeXy;
            v111 = _data[num];
            v211 = _data[num + 1];
            v121 = _data[num + _sizeX];
            v221 = _data[num + 1 + _sizeX];
            v112 = _data[num + _sizeXy];
            v212 = _data[num + 1 + _sizeXy];
            v122 = _data[num + _sizeX + _sizeXy];
            v222 = _data[num + 1 + _sizeX + _sizeXy];
        }

        public float Get(int x, int y, int z)
        {
            return _data[x + y * _sizeX + z * _sizeXy];
        }

        public void Set(int x, int y, int z, float value)
        {
            _data[x + y * _sizeX + z * _sizeXy] = value;
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
