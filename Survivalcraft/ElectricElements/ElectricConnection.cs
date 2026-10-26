namespace Game.ElectricElements;

public class ElectricConnection
{
    public CellFace CellFace;

    public int ConnectorFace;

    public ElectricConnectorType ConnectorType;

    public CellFace NeighborCellFace;

    public int NeighborConnectorFace;

    public ElectricConnectorType NeighborConnectorType;

    public required ElectricElement NeighborElectricElement;
}
