namespace Game.Subsystems;

public class SubsystemFenceBlockBehavior : SubsystemBlockBehavior
{
    public override int[] HandledBlocks => [];

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y, z);
        UpdateVariant(cellValue, x, y, z);
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
    {
        UpdateVariant(value, x, y, z);
    }

    private void UpdateVariant(int value, int x, int y, int z)
    {
        var num = Terrain.ExtractContents(value);
        if (BlocksManager.Blocks[num] is not FenceBlock fenceBlock)
        {
            return;
        }

        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x + 1, y, z);
        var cellValue2 = SubsystemTerrain.Terrain.GetCellValue(x - 1, y, z);
        var cellValue3 = SubsystemTerrain.Terrain.GetCellValue(x, y, z + 1);
        var cellValue4 = SubsystemTerrain.Terrain.GetCellValue(x, y, z - 1);
        var num2 = 0;
        if (fenceBlock.ShouldConnectTo(cellValue))
        {
            num2++;
        }

        if (fenceBlock.ShouldConnectTo(cellValue2))
        {
            num2 += 2;
        }

        if (fenceBlock.ShouldConnectTo(cellValue3))
        {
            num2 += 4;
        }

        if (fenceBlock.ShouldConnectTo(cellValue4))
        {
            num2 += 8;
        }

        var data = Terrain.ExtractData(value);
        var value2 = Terrain.ReplaceData(value, FenceBlock.SetVariant(data, num2));
        SubsystemTerrain.ChangeCell(x, y, z, value2);
    }
}
