namespace Game.TerrainSerializers;

public class TerrainContentsGeneratorFlat : ITerrainContentsGenerator
{
    private readonly Vector2? _islandSize;

    private readonly Vector2 _oceanCorner;

    private readonly Vector2 _shoreRoughnessAmplitude;

    private readonly Vector2 _shoreRoughnessFrequency;

    private readonly Vector2 _shoreRoughnessOctaves;

    private readonly float[] _shoreRoughnessOffset = new float[4];

    private readonly SubsystemTerrain _subsystemTerrain;

    private readonly WorldSettings _worldSettings;

    public TerrainContentsGeneratorFlat(SubsystemTerrain subsystemTerrain)
    {
        _subsystemTerrain = subsystemTerrain;
        var subsystemGameInfo = subsystemTerrain.Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _worldSettings = subsystemGameInfo.WorldSettings;
        _oceanCorner = string.CompareOrdinal(subsystemGameInfo.WorldSettings.OriginalSerializationVersion, "2.1") < 0
            ? _oceanCorner = new Vector2(2001f, 2001f)
            : _oceanCorner = new Vector2(-199f, -199f);
        _islandSize = _worldSettings.TerrainGenerationMode == TerrainGenerationMode.FlatIsland
            ? new Vector2?(_worldSettings.IslandSize)
            : null;
        _shoreRoughnessAmplitude.X = MathUtils.Pow(_worldSettings.ShoreRoughness, 2f) *
                                     (_islandSize.HasValue ? MathUtils.Min(4f * _islandSize.Value.X, 400f) : 400f);
        _shoreRoughnessAmplitude.Y = MathUtils.Pow(_worldSettings.ShoreRoughness, 2f) *
                                     (_islandSize.HasValue ? MathUtils.Min(4f * _islandSize.Value.Y, 400f) : 400f);
        _shoreRoughnessFrequency = MathUtils.Lerp(0.5f, 1f, _worldSettings.ShoreRoughness) * new Vector2(1f) /
                                   _shoreRoughnessAmplitude;
        _shoreRoughnessOctaves.X =
            (int)MathUtils.Clamp(MathUtils.Log(1f / _shoreRoughnessFrequency.X) / MathUtils.Log(2f) - 1f, 1f, 7f);
        _shoreRoughnessOctaves.Y =
            (int)MathUtils.Clamp(MathUtils.Log(1f / _shoreRoughnessFrequency.Y) / MathUtils.Log(2f) - 1f, 1f, 7f);
        var random = new Random(subsystemGameInfo.WorldSeed);
        _shoreRoughnessOffset[0] = random.Float(-2000f, 2000f);
        _shoreRoughnessOffset[1] = random.Float(-2000f, 2000f);
        _shoreRoughnessOffset[2] = random.Float(-2000f, 2000f);
        _shoreRoughnessOffset[3] = random.Float(-2000f, 2000f);
    }

    public int OceanLevel => _worldSettings.TerrainLevel + _worldSettings.SeaLevelOffset;

    public Vector3 FindCoarseSpawnPosition()
    {
        for (var i = -400; i <= 400; i += 10)
        for (var j = -400; j <= 400; j += 10)
        {
            var vector = _oceanCorner + new Vector2(i, j);
            var num = CalculateOceanShoreDistance(vector.X, vector.Y);
            if (num >= 1f && num <= 20f)
            {
                return new Vector3(vector.X, CalculateHeight(vector.X, vector.Y), vector.Y);
            }
        }

        return new Vector3(_oceanCorner.X, CalculateHeight(_oceanCorner.X, _oceanCorner.Y), _oceanCorner.Y);
    }

    public void GenerateChunkContentsPass1(TerrainChunk chunk)
    {
        for (var i = 0; i < 16; i++)
        for (var j = 0; j < 16; j++)
        {
            var num = i + chunk.Origin.X;
            var num2 = j + chunk.Origin.Y;
            chunk.SetTemperatureFast(i, j, CalculateTemperature(num, num2));
            chunk.SetHumidityFast(i, j, CalculateHumidity(num, num2));
            var flag = CalculateOceanShoreDistance(num, num2) >= 0f;
            var num3 = TerrainChunk.CalculateCellIndex(i, 0, j);
            for (var k = 0; k < 256; k++)
            {
                var value = Terrain.MakeBlockValue(0);
                if (flag)
                {
                    if (k < 2)
                    {
                        value = Terrain.MakeBlockValue(1);
                    }
                    else if (k < _worldSettings.TerrainLevel)
                    {
                        value = Terrain.MakeBlockValue(_worldSettings.TerrainBlockIndex == 8
                            ? 2
                            : _worldSettings.TerrainBlockIndex);
                    }
                    else if (k == _worldSettings.TerrainLevel)
                    {
                        value = Terrain.MakeBlockValue(_worldSettings.TerrainBlockIndex);
                    }
                    else if (k <= OceanLevel)
                    {
                        value = Terrain.MakeBlockValue(_worldSettings.TerrainOceanBlockIndex);
                    }
                }
                else if (k < 2)
                {
                    value = Terrain.MakeBlockValue(1);
                }
                else if (k <= OceanLevel)
                {
                    value = Terrain.MakeBlockValue(_worldSettings.TerrainOceanBlockIndex);
                }

                chunk.SetCellValueFast(num3 + k, value);
            }
        }
    }

    public void GenerateChunkContentsPass2(TerrainChunk chunk)
    {
        UpdateFluidIsTop(chunk);
    }

    public void GenerateChunkContentsPass3(TerrainChunk chunk)
    {
    }

    public void GenerateChunkContentsPass4(TerrainChunk chunk)
    {
    }

    public float CalculateOceanShoreDistance(float x, float z)
    {
        var x2 = 0f;
        var x3 = 0f;
        var y = 0f;
        var y2 = 0f;
        if (_shoreRoughnessAmplitude.X > 0f)
        {
            x2 = _shoreRoughnessAmplitude.X * SimplexNoise.OctavedNoise(z + _shoreRoughnessOffset[0],
                _shoreRoughnessFrequency.X, (int)_shoreRoughnessOctaves.X, 2f, 0.6f);
            x3 = _shoreRoughnessAmplitude.X * SimplexNoise.OctavedNoise(z + _shoreRoughnessOffset[1],
                _shoreRoughnessFrequency.X, (int)_shoreRoughnessOctaves.X, 2f, 0.6f);
        }

        if (_shoreRoughnessAmplitude.Y > 0f)
        {
            y = _shoreRoughnessAmplitude.Y * SimplexNoise.OctavedNoise(x + _shoreRoughnessOffset[2],
                _shoreRoughnessFrequency.Y, (int)_shoreRoughnessOctaves.Y, 2f, 0.6f);
            y2 = _shoreRoughnessAmplitude.Y * SimplexNoise.OctavedNoise(x + _shoreRoughnessOffset[3],
                _shoreRoughnessFrequency.Y, (int)_shoreRoughnessOctaves.Y, 2f, 0.6f);
        }

        var vector = _oceanCorner + new Vector2(x2, y);
        var vector2 = _oceanCorner + (_islandSize ?? new Vector2(3.40282347E+38f)) +
                      new Vector2(x3, y2);
        return MathUtils.Min(x - vector.X, vector2.X - x, z - vector.Y, vector2.Y - z);
    }

    public float CalculateHeight(float x, float z)
    {
        return _worldSettings.TerrainLevel;
    }

    public int CalculateTemperature(float x, float z)
    {
        return MathUtils.Clamp(12 + (int)_worldSettings.TemperatureOffset, 0, 15);
    }

    public int CalculateHumidity(float x, float z)
    {
        return MathUtils.Clamp(12 + (int)_worldSettings.HumidityOffset, 0, 15);
    }

    public float CalculateMountainRangeFactor(float x, float z)
    {
        return 0f;
    }

    public void UpdateFluidIsTop(TerrainChunk chunk)
    {
        _ = _subsystemTerrain.Terrain;
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
                if (num4 != 0 && num4 != num2 && BlocksManager.Blocks[num4] is FluidBlock)
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
}
