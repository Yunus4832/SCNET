namespace Game.ElectricElements;

public class WireDomainElectricElement(
    SubsystemElectricity subsystemElectricity,
    IEnumerable<CellFace> cellFaces
) : ElectricElement(subsystemElectricity, cellFaces)
{
    private float _voltage;

    public override float GetOutputVoltage(int face)
    {
        return _voltage;
    }

    public override bool Simulate()
    {
        var voltage = _voltage;
        var num = 0;
        foreach (var connection in Connections)
        {
            if (connection.ConnectorType != ElectricConnectorType.Output && connection.NeighborConnectorType != 0)
            {
                num |= (int)MathUtils.Round(
                    connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace) * 15f);
            }
        }

        _voltage = num / 15f;
        return _voltage.UncloseTo(voltage);
    }

    public override void OnNeighborBlockChanged(CellFace cellFace, int neighborX, int neighborY, int neighborZ)
    {
        var cellValue = SubsystemElectricity.SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
        var num = Terrain.ExtractContents(cellValue);
        if (!(BlocksManager.Blocks[num] is WireBlock))
        {
            return;
        }

        var wireFacesBitmask = WireBlock.GetWireFacesBitmask(cellValue);
        var num2 = wireFacesBitmask;
        if (WireBlock.WireExistsOnFace(cellValue, cellFace.Face))
        {
            var point = CellFace.FaceToPoint3(cellFace.Face);
            var cellValue2 = SubsystemElectricity.SubsystemTerrain.Terrain.GetCellValue(cellFace.X - point.X,
                cellFace.Y - point.Y, cellFace.Z - point.Z);
            var block = BlocksManager.Blocks[Terrain.ExtractContents(cellValue2)];
            if (!block.Collidable || block.Transparent)
            {
                num2 &= ~(1 << cellFace.Face);
            }
        }

        if (num2 == 0)
        {
            SubsystemElectricity.SubsystemTerrain.DestroyCell(0, cellFace.X, cellFace.Y, cellFace.Z, 0, false, false);
        }
        else if (num2 != wireFacesBitmask)
        {
            var newValue = WireBlock.SetWireFacesBitmask(cellValue, num2);
            SubsystemElectricity.SubsystemTerrain.DestroyCell(0, cellFace.X, cellFace.Y, cellFace.Z, newValue, false,
                false);
        }
    }
}
