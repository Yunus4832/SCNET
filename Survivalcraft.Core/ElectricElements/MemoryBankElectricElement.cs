namespace Game.ElectricElements;

public class MemoryBankElectricElement : RotatableElectricElement
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
        var readHigh = false;
        var hasControlInput = false;
        var writeHigh = false;
        var writeVoltage = 0f;
        var addressLow = 0;
        var addressHigh = 0;
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
                        addressLow = (int)MathUtils.Round(
                            connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace) *
                            15f
                        );
                    }
                    else if (connectorDirection == ElectricConnectorDirection.Left)
                    {
                        addressHigh = (int)MathUtils.Round(
                            connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace) *
                            15f
                        );
                    }
                    else if (connectorDirection == ElectricConnectorDirection.Bottom)
                    {
                        var controlBits = (int)MathUtils.Round(
                            connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace) *
                            15f
                        );
                        readHigh = controlBits >= 8;
                        writeHigh = controlBits is > 0 and < 8;
                        hasControlInput = true;
                    }
                    else if (connectorDirection == ElectricConnectorDirection.In)
                    {
                        writeVoltage =
                            connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace);
                    }
                }
            }
        }

        var memoryBankData = _subsystemMemoryBankBlockBehavior.GetBlockData(CellFaces[0].Point);
        var address = addressLow + (addressHigh << 4);
        if (hasControlInput)
        {
            if (readHigh && _clockAllowed)
            {
                _clockAllowed = false;
                _voltage = memoryBankData != null ? memoryBankData.Read(address) / 15f : 0f;
            }
            else if (writeHigh && _writeAllowed)
            {
                _writeAllowed = false;
                if (memoryBankData == null)
                {
                    memoryBankData = new MemoryBankData();
                    _subsystemMemoryBankBlockBehavior.SetBlockData(CellFaces[0].Point, memoryBankData);
                }

                memoryBankData.Write(address, (byte)MathUtils.Round(writeVoltage * 15f));
            }
        }
        else
        {
            _voltage = memoryBankData != null ? memoryBankData.Read(address) / 15f : 0f;
        }

        if (!readHigh)
        {
            _clockAllowed = true;
        }

        if (!writeHigh)
        {
            _writeAllowed = true;
        }

        memoryBankData?.LastOutput = (byte)MathUtils.Round(_voltage * 15f);
        return _voltage.UncloseTo(voltage);
    }
}
