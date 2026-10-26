using Engine.Core.Mathematics;

using Game.Components;
using Game.ElectricElements;
using Game.Protocol.Terrain;
using Game.Subsystems;
using Game.Terrains;

namespace BeaconMod;

public sealed class BeaconElectricElement(SubsystemElectricity electricity, CellFace cellFace)
    : MountedElectricElement(electricity, cellFace)
{
    public static int IntensityFromVoltage(float voltage) => Math.Clamp((int)MathF.Round(voltage * 15), 0, 15);

    public override bool Simulate()
    {
        var voltage = 0f;
        foreach (var connection in Connections)
        {
            if (connection.ConnectorType != ElectricConnectorType.Output &&
                connection.NeighborConnectorType != ElectricConnectorType.Input)
            {
                voltage = Math.Max(
                    voltage,
                    connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace)
                );
            }
        }

        var point = CellFaces[0].Point;
        var terrain = SubsystemElectricity.SubsystemTerrain;
        var value = terrain.Terrain.GetCellValue(point.X, point.Y, point.Z);
        var data = Terrain.ExtractData(value);
        var next = BeaconBlock.SetIntensity(data, IntensityFromVoltage(voltage));
        if (data != next)
        {
            terrain.ChangeCell(point.X, point.Y, point.Z, Terrain.ReplaceData(value, next));
        }

        return false;
    }

    public override bool OnInteract(TerrainRaycastResult hit, ComponentMiner miner)
    {
        var point = CellFaces[0].Point;
        var terrain = SubsystemElectricity.SubsystemTerrain;
        var value = terrain.Terrain.GetCellValue(point.X, point.Y, point.Z);
        terrain.ChangeCell(
            point.X,
            point.Y,
            point.Z,
            Terrain.ReplaceData(value, BeaconBlock.CycleColor(Terrain.ExtractData(value)))
        );
        SubsystemElectricity.SubsystemAudio.PlaySound("Audio/Click", 1, 0, new Vector3(point), 2, true);
        return true;
    }
}
