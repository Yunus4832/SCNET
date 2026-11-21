namespace Game.Terrains.Generation;

public partial class TerrainContentsGenerator : GeneratedTerrainEnvironment, ITerrainContentsGenerator
{
    private readonly SubsystemBottomSuckerBlockBehavior _subsystemBottomSuckerBlockBehavior;

    private readonly SubsystemTerrain _subsystemTerrain;

    private readonly Terrain _terrain;

    private readonly Block[] _generationBlocks;

    static TerrainContentsGenerator()
    {
        CreateBrushes();
    }

    public TerrainContentsGenerator(SubsystemTerrain subsystemTerrain, Terrain? terrain = null)
        : base(subsystemTerrain.SubsystemGameInfo.WorldSettings, subsystemTerrain.SubsystemGameInfo.WorldSeed)
    {
        _subsystemTerrain = subsystemTerrain;
        _terrain = terrain ?? subsystemTerrain.Terrain;
        _generationBlocks = subsystemTerrain.Catalog.Blocks;
        _subsystemBottomSuckerBlockBehavior =
            subsystemTerrain.Project.FindSubsystem<SubsystemBottomSuckerBlockBehavior>(true)!;
    }

    private TerrainContentsGenerator(TerrainContentsGenerator owner, Terrain terrain, WorldSettings settings)
        : base(settings, owner.seed)
    {
        _subsystemTerrain = owner._subsystemTerrain;
        _subsystemBottomSuckerBlockBehavior = owner._subsystemBottomSuckerBlockBehavior;
        _generationBlocks = owner._generationBlocks;
        _terrain = terrain;
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
        if (tgExtras) { RequirePreparedTrees(chunk); }

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
        _preparedTrees.Remove(chunk);
    }
}
