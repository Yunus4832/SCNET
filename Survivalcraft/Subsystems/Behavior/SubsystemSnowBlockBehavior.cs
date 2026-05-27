namespace Game.Subsystems;

public class SubsystemSnowBlockBehavior : SubsystemBlockBehavior
{
    public override int[] HandledBlocks => [61];

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
        return BlocksManager.Blocks[num] is FallenLeavesBlock;
    }

    public static bool CanSupportSnow(int value)
    {
        var num = Terrain.ExtractContents(value);
        var block = BlocksManager.Blocks[num];
        if (block.Transparent)
        {
            return block is LeavesBlock;
        }

        return true;
    }
}
