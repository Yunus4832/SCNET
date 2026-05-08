namespace Game.ElectricElements;

public class MemoryBankElectricElement : RotateableElectricElement
{
    private bool _clockAllowed;

    private readonly SubsystemMemoryBankBlockBehavior _subsystemMemoryBankBlockBehavior;

    private float _voltage;

    private bool _writeAllowed;

    public MemoryBankElectricElement(
        SubsystemElectricity subsystemElectricity,
        CellFace cellFace
    ) : base(subsystemElectricity, cellFace)
    {
        _subsystemMemoryBankBlockBehavior =
            subsystemElectricity.Project.FindSubsystem<SubsystemMemoryBankBlockBehavior>(true)!;
        var blockData = _subsystemMemoryBankBlockBehavior.GetBlockData(cellFace.Point);
        if (blockData != null)
        {
            _voltage = blockData.LastOutput / 15f;
        }
    }

    public override float GetOutputVoltage(int face)
    {
        return _voltage;
    }

    public override bool Simulate()
    {
        var voltage = _voltage;
        var flag = false;
        var flag2 = false;
        var flag3 = false;
        var num = 0f;
        var num2 = 0;
        var num3 = 0;
        var rotation = Rotation;
        foreach (var connection in Connections)
        {
            if (connection.ConnectorType != ElectricConnectorType.Output && connection.NeighborConnectorType != 0)
            {
                var connectorDirection =
                    SubsystemElectricity.GetConnectorDirection(CellFaces[0].Face, rotation, connection.ConnectorFace);
                if (connectorDirection.HasValue)
                {
                    if (connectorDirection == ElectricConnectorDirection.Right)
                    {
                        num2 = (int)MathUtils.Round(
                            connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace) *
                            15f);
                    }
                    else if (connectorDirection == ElectricConnectorDirection.Left)
                    {
                        num3 = (int)MathUtils.Round(
                            connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace) *
                            15f);
                    }
                    else if (connectorDirection == ElectricConnectorDirection.Bottom)
                    {
                        var num4 = (int)MathUtils.Round(
                            connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace) *
                            15f);
                        flag = num4 >= 8;
                        flag3 = num4 is > 0 and < 8;
                        flag2 = true;
                    }
                    else if (connectorDirection == ElectricConnectorDirection.In)
                    {
                        num = connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace);
                    }
                }
            }
        }

        var memoryBankData = _subsystemMemoryBankBlockBehavior.GetBlockData(CellFaces[0].Point);
        var address = num2 + (num3 << 4);
        if (flag2)
        {
            if (flag && _clockAllowed)
            {
                _clockAllowed = false;
                _voltage = memoryBankData != null ? memoryBankData.Read(address) / 15f : 0f;
            }
            else if (flag3 && _writeAllowed)
            {
                _writeAllowed = false;
                if (memoryBankData == null)
                {
                    memoryBankData = new MemoryBankData();
                    _subsystemMemoryBankBlockBehavior.SetBlockData(CellFaces[0].Point, memoryBankData);
                }

                memoryBankData.Write(address, (byte)MathUtils.Round(num * 15f));
            }
        }
        else
        {
            _voltage = memoryBankData != null ? memoryBankData.Read(address) / 15f : 0f;
        }

        if (!flag)
        {
            _clockAllowed = true;
        }

        if (!flag3)
        {
            _writeAllowed = true;
        }

        memoryBankData?.LastOutput = (byte)MathUtils.Round(_voltage * 15f);
        return _voltage.UncloseTo(voltage);
    }
}
