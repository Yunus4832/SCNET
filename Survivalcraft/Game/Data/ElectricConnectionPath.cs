namespace Game;

public class ElectricConnectionPath(
    int neighborOffsetX,
    int neighborOffsetY,
    int neighborOffsetZ,
    int neighborFace,
    int connectorFace,
    int neighborConnectorFace
)
{
    public readonly int ConnectorFace = connectorFace;

    public readonly int NeighborConnectorFace = neighborConnectorFace;

    public readonly int NeighborFace = neighborFace;

    public readonly int NeighborOffsetX = neighborOffsetX;

    public readonly int NeighborOffsetY = neighborOffsetY;

    public readonly int NeighborOffsetZ = neighborOffsetZ;
}
