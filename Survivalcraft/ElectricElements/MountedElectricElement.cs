namespace Game.ElectricElements;

public abstract class MountedElectricElement(
    SubsystemElectricity subsystemElectricity,
    CellFace cellFace
) : ElectricElement(subsystemElectricity, cellFace)
{
    public override void OnNeighborBlockChanged(CellFace cellFace, int neighborX, int neighborY, int neighborZ)
    {
        var point = CellFace.FaceToPoint3(cellFace.Face);
        var x = cellFace.X - point.X;
        var y = cellFace.Y - point.Y;
        var z = cellFace.Z - point.Z;
        if (!SubsystemElectricity.SubsystemTerrain.Terrain.IsCellValid(x, y, z))
        {
            return;
        }

        var cellValue = SubsystemElectricity.SubsystemTerrain.Terrain.GetCellValue(x, y, z);
        var block = BlocksManager.Blocks[Terrain.ExtractContents(cellValue)];
        if ((!block.Collidable ||
             block.IsFaceTransparent(SubsystemElectricity.SubsystemTerrain, cellFace.Face, cellValue)) &&
            (cellFace.Face != 4 || block is not FenceBlock))
        {
            SubsystemElectricity.SubsystemTerrain.DestroyCell(
                0,
                cellFace.X,
                cellFace.Y,
                cellFace.Z,
                0,
                false,
                false
            );
        }
    }
}
