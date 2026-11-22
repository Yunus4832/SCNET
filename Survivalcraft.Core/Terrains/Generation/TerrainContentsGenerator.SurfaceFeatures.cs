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
                var worldX = i + chunk.Origin.X;
                var worldZ = j + chunk.Origin.Y;
                var basinNoise = SimplexNoise.OctavedNoise(
                    worldX + temperatureOffset.X,
                    worldZ + temperatureOffset.Y,
                    0.001f / tgBiomeScaling,
                    2,
                    2.9f,
                    2f
                );
                var isLowland = basinNoise < 0.29; //代表着原本就是低洼地带
                if (!isLowland)
                {
                    continue;
                }

                var depthFactor = MathUtils.Pow((0.3f - basinNoise + 0.01f) * 125f, 2f) / 100f;
                var exceedsMaxDepth = depthFactor > 1f;
                if (exceedsMaxDepth)
                {
                    depthFactor = 1f;
                }

                var carveDepth = (int)(depthFactor * 10f); //深度
                var surfaceContents = 0;
                var waterDepth = 0;
                var foundSurface = false;
                for (var k = 245; k > 0; k--)
                {
                    var cellContentsFast = chunk.GetCellContentsFast(i, k, j);
                    var isOccupied = cellContentsFast != 0;
                    if (!isOccupied)
                    {
                        continue;
                    }

                    var l = 0;
                    while (l < carveDepth)
                    {
                        var isSurfaceWater = chunk.GetCellContentsFast(i, k, j) == 18 && !foundSurface;
                        if (isSurfaceWater)
                        {
                            waterDepth++;
                        }
                        else
                        {
                            var needsSurfaceContents = !foundSurface;
                            if (needsSurfaceContents)
                            {
                                foundSurface = true;
                                surfaceContents = chunk.GetCellContentsFast(i, k, j);
                            }
                        }

                        chunk.SetCellValueFast(i, k, j, 0);
                        l++;
                        k--;
                    }

                    var restoreWater = waterDepth > 1;
                    if (restoreWater)
                    {
                        var m = 0;
                        while (m < waterDepth)
                        {
                            chunk.SetCellValueFast(i, k, j, 18);
                            m++;
                            k--;
                        }
                    }

                    var restoreSurface = foundSurface;
                    if (restoreSurface)
                    {
                        chunk.SetCellValueFast(i, k, j, surfaceContents);
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
                var worldX = i + chunk.Origin.X;
                var worldZ = j + chunk.Origin.Y;
                float bedrockThickness = 2 + (int)(4f * SimplexNoise.OctavedNoise(worldX, worldZ, 0.1f, 1, 1f, 1f));
                for (var k = 0; k < bedrockThickness; k++)
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
                var worldX = i + chunk.Origin.X;
                var worldZ = j + chunk.Origin.Y;
                for (var surfaceY = 254; surfaceY >= 0; surfaceY--)
                {
                    var cellValueFast = chunk.GetCellValueFast(i, surfaceY, j);
                    var contents = Terrain.ExtractContents(cellValueFast);
                    if (contents != 0)
                    {
                        if (!SubsystemWeather.IsPlaceFrozen(chunk.GetTemperatureFast(i, j), surfaceY))
                        {
                            break;
                        }

                        if (_subsystemTerrain.Catalog.Blocks[contents] is WaterBlock)
                        {
                            if (CalculateOceanShoreDistance(worldX, worldZ) > -20f)
                            {
                                float iceThickness = 1 +
                                                     (int)(2f *
                                                           MathUtils.Sqr(
                                                               SimplexNoise.OctavedNoise(
                                                                   worldX,
                                                                   worldZ,
                                                                   0.2f,
                                                                   1,
                                                                   2f,
                                                                   1f
                                                               )
                                                           ));
                                for (var k = 0; k < iceThickness; k++)
                                {
                                    if (surfaceY - k <= 0)
                                    {
                                        continue;
                                    }

                                    if (!(_subsystemTerrain.Catalog.Blocks[
                                            chunk.GetCellContentsFast(i, surfaceY - k, j)] is WaterBlock))
                                    {
                                        break;
                                    }

                                    chunk.SetCellValueFast(i, surfaceY - k, j, 62);
                                }

                                if (SubsystemWeather.ShaftHasSnowOnIce(worldX, worldZ))
                                {
                                    chunk.SetCellValueFast(i, surfaceY + 1, j, 61);
                                }
                            }
                        }
                        else if (SubsystemSnowBlockBehavior.CanSupportSnow(_subsystemTerrain.Catalog, cellValueFast))
                        {
                            chunk.SetCellValueFast(i, surfaceY + 1, j, 61);
                        }

                        if (contents == 8)
                        {
                            chunk.SetCellValueFast(i, surfaceY, j, Terrain.MakeBlockValue(8, 1));
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
