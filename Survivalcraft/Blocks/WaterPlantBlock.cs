namespace Game.Blocks;

public abstract class WaterPlantBlock : WaterBlock
{
    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var point = raycastResult.CellFace.Point + CellFace.FaceToPoint3(raycastResult.CellFace.Face);
        var cellValue = subsystemTerrain.Terrain.GetCellValue(point.X, point.Y, point.Z);
        var num = Terrain.ExtractContents(cellValue);
        var data = Terrain.ExtractData(cellValue);
        BlockPlacementData result;
        if (BlocksManager.Blocks[num] is WaterBlock)
        {
            result = default;
            result.CellFace = raycastResult.CellFace;
            result.Value = Terrain.MakeBlockValue(BlockIndex, 0, data);
            return result;
        }

        result = default;
        return result;
    }
}
