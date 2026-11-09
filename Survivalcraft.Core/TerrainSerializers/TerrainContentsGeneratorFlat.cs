namespace Game.TerrainSerializers;

public class TerrainContentsGeneratorFlat : TerrainEnvironmentFlat, ITerrainContentsGenerator
{
    private readonly SubsystemTerrain _subsystemTerrain;

    private readonly Terrain _terrain;

    public TerrainContentsGeneratorFlat(SubsystemTerrain subsystemTerrain, Terrain? terrain = null)
        : base(subsystemTerrain.SubsystemGameInfo.WorldSettings, subsystemTerrain.SubsystemGameInfo.WorldSeed)
    {
        _subsystemTerrain = subsystemTerrain;
        _terrain = terrain ?? subsystemTerrain.Terrain;
    }

    public Vector3 FindCoarseSpawnPosition()
    {
        for (var i = -400; i <= 400; i += 10)
        {
            for (var j = -400; j <= 400; j += 10)
            {
                var vector = oceanCorner + new Vector2(i, j);
                var num = CalculateOceanShoreDistance(vector.X, vector.Y);
                if (num is >= 1f and <= 20f)
                {
                    return new Vector3(vector.X, CalculateHeight(vector.X, vector.Y), vector.Y);
                }
            }
        }

        return new Vector3(oceanCorner.X, CalculateHeight(oceanCorner.X, oceanCorner.Y), oceanCorner.Y);
    }

    public void GenerateChunkContentsPass1(TerrainChunk chunk)
    {
        for (var i = 0; i < 16; i++)
        {
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
                        else if (k < worldSettings.TerrainLevel)
                        {
                            value = Terrain.MakeBlockValue(
                                worldSettings.TerrainBlockIndex == 8
                                    ? 2
                                    : worldSettings.TerrainBlockIndex
                            );
                        }
                        else if (k == worldSettings.TerrainLevel)
                        {
                            value = Terrain.MakeBlockValue(worldSettings.TerrainBlockIndex);
                        }
                        else if (k <= OceanLevel)
                        {
                            value = Terrain.MakeBlockValue(worldSettings.TerrainOceanBlockIndex);
                        }
                    }
                    else if (k < 2)
                    {
                        value = Terrain.MakeBlockValue(1);
                    }
                    else if (k <= OceanLevel)
                    {
                        value = Terrain.MakeBlockValue(worldSettings.TerrainOceanBlockIndex);
                    }

                    chunk.SetCellValueFast(num3 + k, value);
                }
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

    public void UpdateFluidIsTop(TerrainChunk chunk)
    {
        _ = _terrain;
        for (var i = 0; i < 16; i++)
        {
            for (var j = 0; j < 16; j++)
            {
                var num = TerrainChunk.CalculateCellIndex(i, 255, j);
                var num2 = 0;
                var num3 = 255;
                while (num3 >= 0)
                {
                    var cellValueFast = chunk.GetCellValueFast(num);
                    var num4 = Terrain.ExtractContents(cellValueFast);
                    if (num4 != 0 &&
                        num4 != num2 &&
                        _subsystemTerrain.Catalog.Blocks[num4] is FluidBlock)
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
}
