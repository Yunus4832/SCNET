namespace Game.Terrains.Generation;

public partial class TerrainContentsGenerator
{
    public void GenerateBasin(TerrainChunk chunk) //盆地
    {
        var x = chunk.Origin.X;
        var y = chunk.Origin.Y;
        if (CalculateHeight(x, y) < 66)
        {
            return;
        }

        for (var i = 0; i < 16; i++)
        {
            for (var j = 0; j < 16; j++)
            {
                var num = i + chunk.Origin.X;
                var num2 = j + chunk.Origin.Y;
                var num3 = SimplexNoise.OctavedNoise(
                    num + temperatureOffset.X,
                    num2 + temperatureOffset.Y,
                    0.001f / tgBiomeScaling,
                    2,
                    2.9f,
                    2f
                );
                var flag = num3 < 0.29; //代表着原本就是低洼地带
                if (!flag)
                {
                    continue;
                }

                var num4 = MathUtils.Pow((0.3f - num3 + 0.01f) * 125f, 2f) / 100f;
                var flag2 = num4 > 1f;
                if (flag2)
                {
                    num4 = 1f;
                }

                var num5 = (int)(num4 * 10f); //深度
                var num6 = 0;
                var num7 = 0;
                var flag3 = false;
                for (var k = 245; k > 0; k--)
                {
                    var cellContentsFast = chunk.GetCellContentsFast(i, k, j);
                    var flag4 = cellContentsFast != 0;
                    if (!flag4)
                    {
                        continue;
                    }

                    var l = 0;
                    while (l < num5)
                    {
                        var flag5 = chunk.GetCellContentsFast(i, k, j) == 18 && !flag3;
                        if (flag5)
                        {
                            num7++;
                        }
                        else
                        {
                            var flag6 = !flag3;
                            if (flag6)
                            {
                                flag3 = true;
                                num6 = chunk.GetCellContentsFast(i, k, j);
                            }
                        }

                        chunk.SetCellValueFast(i, k, j, 0);
                        l++;
                        k--;
                    }

                    var flag7 = num7 > 1;
                    if (flag7)
                    {
                        var m = 0;
                        while (m < num7)
                        {
                            chunk.SetCellValueFast(i, k, j, 18);
                            m++;
                            k--;
                        }
                    }

                    var flag8 = flag3;
                    if (flag8)
                    {
                        chunk.SetCellValueFast(i, k, j, num6);
                    }

                    break;
                }
            }
        }
    }

    public void GenerateBedrockAndAir(TerrainChunk chunk)
    {
        var value = Terrain.MakeBlockValue(1);
        for (var i = 0; i < 16; i++)
        {
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
    }

    public void GenerateSnowAndIce(TerrainChunk chunk)
    {
        for (var i = 0; i < 16; i++)
        {
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

                        if (_subsystemTerrain.Catalog.Blocks[num4] is WaterBlock)
                        {
                            if (CalculateOceanShoreDistance(num, num2) > -20f)
                            {
                                float num5 = 1 +
                                             (int)(2f *
                                                   MathUtils.Sqr(
                                                       SimplexNoise.OctavedNoise(
                                                           num,
                                                           num2,
                                                           0.2f,
                                                           1,
                                                           2f,
                                                           1f
                                                       )
                                                   ));
                                for (var k = 0; k < num5; k++)
                                {
                                    if (num3 - k <= 0)
                                    {
                                        continue;
                                    }

                                    if (!(_subsystemTerrain.Catalog.Blocks[
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
                        else if (SubsystemSnowBlockBehavior.CanSupportSnow(_subsystemTerrain.Catalog, cellValueFast))
                        {
                            chunk.SetCellValueFast(i, num3 + 1, j, 61);
                        }

                        if (num4 == 8)
                        {
                            chunk.SetCellValueFast(i, num3, j, Terrain.MakeBlockValue(8, 1));
                        }

                        break;
                    }
                }
            }
        }
    }

    public void PropagateFluidsDownwards(TerrainChunk chunk)
    {
        for (var localX = 0; localX < 16; localX++)
        {
            for (var localZ = 0; localZ < 16; localZ++)
            {
                var cellIndex = TerrainChunk.CalculateCellIndex(localX, 255, localZ);
                var contentsAbove = 0;
                var y = 255;
                while (y >= 0)
                {
                    var contents = Terrain.ExtractContents(chunk.GetCellValueFast(cellIndex));
                    if (contents == 0 &&
                        contentsAbove != 0 &&
                        _subsystemTerrain.Catalog.FluidBlocks[contentsAbove] != null)
                    {
                        chunk.SetCellValueFast(cellIndex, contentsAbove);
                        contents = contentsAbove;
                    }

                    contentsAbove = contents;
                    y--;
                    cellIndex--;
                }
            }
        }
    }

    public void UpdateFluidIsTop(TerrainChunk chunk)
    {
        for (var localX = 0; localX < 16; localX++)
        {
            for (var localZ = 0; localZ < 16; localZ++)
            {
                var cellIndex = TerrainChunk.CalculateCellIndex(localX, 255, localZ);
                var contentsAbove = 0;
                var y = 255;
                while (y >= 0)
                {
                    var cellValue = chunk.GetCellValueFast(cellIndex);
                    var contents = Terrain.ExtractContents(cellValue);
                    if (contents != contentsAbove &&
                        _subsystemTerrain.Catalog.FluidBlocks[contents] != null &&
                        _subsystemTerrain.Catalog.FluidBlocks[contentsAbove] == null)
                    {
                        var data = Terrain.ExtractData(cellValue);
                        chunk.SetCellValueFast(
                            cellIndex,
                            Terrain.MakeBlockValue(contents, FluidBlock.SetIsTop(data, true))
                        );
                    }

                    contentsAbove = contents;
                    y--;
                    cellIndex--;
                }
            }
        }
    }
}
