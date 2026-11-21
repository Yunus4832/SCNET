namespace Game.Terrains.Generation;

public class TerrainContentsGeneratorFlat : TerrainEnvironmentFlat, ITerrainContentsGenerator
{
    private readonly SubsystemTerrain _subsystemTerrain;

    private readonly int _surfaceBlock;

    private readonly int _oceanBlock;

    public TerrainContentsGeneratorFlat(SubsystemTerrain subsystemTerrain, Terrain? terrain = null)
        : base(subsystemTerrain.SubsystemGameInfo.WorldSettings, subsystemTerrain.SubsystemGameInfo.WorldSeed)
    {
        _subsystemTerrain = subsystemTerrain;
        var parameters = subsystemTerrain.SubsystemGameInfo.WorldSettings.Generator;
        _surfaceBlock = ResolveBlock(parameters.Get<string>("surfaceBlock"));
        _oceanBlock = ResolveBlock(parameters.Get<string>("oceanBlock"));

        int ResolveBlock(string id)
        {
            return subsystemTerrain.Catalog.Registry.TryGet(ResourceId.Parse(id), out var entry)
                ? entry!.RuntimeIndex
                : throw new InvalidDataException($"Unregistered terrain block '{id}'.");
        }
    }

    public Vector3 FindCoarseSpawnPosition()
    {
        for (var offsetX = -400; offsetX <= 400; offsetX += 10)
        {
            for (var offsetZ = -400; offsetZ <= 400; offsetZ += 10)
            {
                var candidate = oceanCorner + new Vector2(offsetX, offsetZ);
                var shoreDistance = CalculateOceanShoreDistance(candidate.X, candidate.Y);
                if (shoreDistance is >= 1f and <= 20f)
                {
                    return new Vector3(candidate.X, CalculateHeight(candidate.X, candidate.Y), candidate.Y);
                }
            }
        }

        return new Vector3(oceanCorner.X, CalculateHeight(oceanCorner.X, oceanCorner.Y), oceanCorner.Y);
    }

    public void GenerateChunkContentsPass1(TerrainChunk chunk)
    {
        for (var localX = 0; localX < 16; localX++)
        {
            for (var localZ = 0; localZ < 16; localZ++)
            {
                var worldX = localX + chunk.Origin.X;
                var worldZ = localZ + chunk.Origin.Y;
                chunk.SetTemperatureFast(localX, localZ, CalculateTemperature(worldX, worldZ));
                chunk.SetHumidityFast(localX, localZ, CalculateHumidity(worldX, worldZ));
                var isLand = CalculateOceanShoreDistance(worldX, worldZ) >= 0f;
                var columnIndex = TerrainChunk.CalculateCellIndex(localX, 0, localZ);
                for (var y = 0; y < 256; y++)
                {
                    var value = Terrain.MakeBlockValue(0);
                    if (isLand)
                    {
                        if (y < 2)
                        {
                            value = Terrain.MakeBlockValue(1);
                        }
                        else if (y < terrainHeight)
                        {
                            value = Terrain.MakeBlockValue(
                                _surfaceBlock == 8
                                    ? 2
                                    : _surfaceBlock
                            );
                        }
                        else if (y == terrainHeight)
                        {
                            value = Terrain.MakeBlockValue(_surfaceBlock);
                        }
                        else if (y <= OceanLevel)
                        {
                            value = Terrain.MakeBlockValue(_oceanBlock);
                        }
                    }
                    else if (y < 2)
                    {
                        value = Terrain.MakeBlockValue(1);
                    }
                    else if (y <= OceanLevel)
                    {
                        value = Terrain.MakeBlockValue(_oceanBlock);
                    }

                    chunk.SetCellValueFast(columnIndex + y, value);
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
                    if (contents != 0 &&
                        contents != contentsAbove &&
                        _subsystemTerrain.Catalog.Blocks[contents] is FluidBlock)
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
