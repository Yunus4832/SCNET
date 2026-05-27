namespace Game.Subsystems;

public class SubsystemGravestoneBlockBehavior : SubsystemBlockBehavior
{
    public override int[] HandledBlocks => [189];

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        var cellContents = SubsystemTerrain.Terrain.GetCellContents(x, y - 1, z);
        if (BlocksManager.Blocks[cellContents].Transparent)
        {
            SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
        }
    }
}
