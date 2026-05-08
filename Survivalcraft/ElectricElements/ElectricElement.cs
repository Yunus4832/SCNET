using System.Text;

namespace Game.ElectricElements;

public abstract class ElectricElement(
    SubsystemElectricity subsystemElectricity,
    IEnumerable<CellFace> cellFaces
)
{
    public ElectricElement(
        SubsystemElectricity subsystemElectricity,
        CellFace cellFace
    ) : this(
        subsystemElectricity,
        new List<CellFace>
        {
            cellFace
        })
    {
    }

    public SubsystemElectricity SubsystemElectricity { get; set; } = subsystemElectricity;

    public ReadOnlyList<CellFace> CellFaces { get; } = new(new List<CellFace>(cellFaces));

    public List<ElectricConnection> Connections { get; set; } = [];

    public override int GetHashCode()
    {
        var s = new StringBuilder();
        foreach (var cell in CellFaces)
        {
            s.Append(cell.ToString());
        }

        return s.ToString().GetHashCode();
    }

    public virtual float GetOutputVoltage(int face)
    {
        return 0f;
    }

    public virtual bool Simulate()
    {
        return false;
    }

    public virtual void OnAdded()
    {
    }

    public virtual void OnRemoved()
    {
    }

    public virtual void OnNeighborBlockChanged(CellFace cellFace, int neighborX, int neighborY, int neighborZ)
    {
    }

    public virtual bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        return false;
    }

    public virtual void OnCollide(CellFace cellFace, float velocity, ComponentBody componentBody)
    {
    }

    public virtual void OnHitByProjectile(CellFace cellFace, WorldItem worldItem)
    {
    }

    public virtual void OnConnectionsChanged()
    {
    }

    public static bool IsSignalHigh(float voltage)
    {
        return voltage >= 0.5f;
    }

    public int CalculateHighInputsCount()
    {
        var num = 0;
        foreach (var connection in Connections)
        {
            if (connection.ConnectorType != ElectricConnectorType.Output && connection.NeighborConnectorType != 0 &&
                IsSignalHigh(connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace)))
            {
                num++;
            }
        }

        return num;
    }
}
