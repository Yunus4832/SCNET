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
        var combinedBits = 0;
        foreach (var connection in Connections)
        {
            if (connection.ConnectorType != ElectricConnectorType.Output && connection.NeighborConnectorType != 0)
            {
                combinedBits |= (int)MathUtils.Round(
                    connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace) * 15f
                );
            }
        }

        _voltage = combinedBits / 15f;
        return _voltage.UncloseTo(voltage);
    }

    public override void OnNeighborBlockChanged(CellFace cellFace, int neighborX, int neighborY, int neighborZ)
    {
        var cellValue = SubsystemElectricity.SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
        var contents = Terrain.ExtractContents(cellValue);
        if (SubsystemElectricity.SubsystemTerrain.Catalog.Blocks[contents] is not WireBlock wire)
        {
            return;
        }

        var wireFacesBitmask = wire.GetWireFacesBitmask(cellValue);
        var remainingWireFaces = wireFacesBitmask;
        if (wire.WireExistsOnFace(cellValue, cellFace.Face))
        {
            var point = CellFace.FaceToPoint3(cellFace.Face);
            var cellValue2 = SubsystemElectricity.SubsystemTerrain.Terrain.GetCellValue(
                cellFace.X - point.X,
                cellFace.Y - point.Y,
                cellFace.Z - point.Z
            );
            var block = SubsystemElectricity.SubsystemTerrain.Catalog.Blocks[Terrain.ExtractContents(cellValue2)];
            if (!block.Collidable || block.Transparent)
            {
                remainingWireFaces &= ~(1 << cellFace.Face);
            }
        }

        if (remainingWireFaces == 0)
        {
            SubsystemElectricity.SubsystemTerrain.DestroyCell(0, cellFace.X, cellFace.Y, cellFace.Z, 0, false, false);
        }
        else if (remainingWireFaces != wireFacesBitmask)
        {
            var newValue = wire.SetWireFacesBitmask(cellValue, remainingWireFaces);
            SubsystemElectricity.SubsystemTerrain.DestroyCell(
                0,
                cellFace.X,
                cellFace.Y,
                cellFace.Z,
                newValue,
                false,
                false
            );
        }
    }
}
