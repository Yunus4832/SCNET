namespace Game.Subsystems;

public class SubsystemSnowBlockBehavior : SubsystemBlockBehavior
{
    public override ResourceId[] HandledBlocks => [ResourceId.Parse("game:snow")];

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        if (!CanSupportSnow(SubsystemTerrain.Terrain.GetCellValue(x, y - 1, z)))
        {
            SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
        }
    }

    public static bool CanBeReplacedBySnow(int value)
    {
        var num = Terrain.ExtractContents(value);
        return CurrentModRuntime.Value!.RequireWorldCatalog().Blocks[num] is FallenLeavesBlock;
    }

    public static bool CanSupportSnow(int value)
    {
        var num = Terrain.ExtractContents(value);
        var block = CurrentModRuntime.Value!.RequireWorldCatalog().Blocks[num];
        if (block.Transparent)
        {
            return block is LeavesBlock;
        }

        return true;
    }
}
