namespace Game.ElectricElements;

public class BatteryElectricElement(
    SubsystemElectricity subsystemElectricity,
    CellFace cellFace
) : ElectricElement(subsystemElectricity, cellFace)
{
    public override float GetOutputVoltage(int face)
    {
        var point = CellFaces[0].Point;
        return BatteryBlock.GetVoltageLevel(
            Terrain.ExtractData(
                SubsystemElectricity.SubsystemTerrain.Terrain.GetCellValue(point.X, point.Y, point.Z))) / 15f;
    }

    public override void OnNeighborBlockChanged(CellFace cellFace, int neighborX, int neighborY, int neighborZ)
    {
        var cellValue =
            SubsystemElectricity.SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y - 1, cellFace.Z);
        var block = BlocksManager.Blocks[Terrain.ExtractContents(cellValue)];
        if (!block.Collidable || block.Transparent)
        {
            SubsystemElectricity.SubsystemTerrain.DestroyCell(0, cellFace.X, cellFace.Y, cellFace.Z, 0, false, false);
        }
    }
}
