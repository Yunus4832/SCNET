namespace Game.Subsystems.Behavior;

public class SubsystemWaterPlantBlockBehavior : SubsystemInWaterBlockBehavior
{
    public override ResourceId[] HandledBlocks => [];

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        base.OnNeighborBlockChanged(x, y, z, neighborX, neighborY, neighborZ);
        var contents = Terrain.ExtractContents(SubsystemTerrain.Terrain.GetCellValue(x, y, z));
        var supportContents = Terrain.ExtractContents(SubsystemTerrain.Terrain.GetCellValue(x, y - 1, z));
        if (supportContents != 2 && supportContents != 7 && supportContents != 72 && supportContents != contents)
        {
            SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
        }
    }
}
