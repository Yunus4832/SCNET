using System.Reflection;

using Engine.Core.Mathematics;

using Game.Blocks;
using Game.ElectricElements;
using Game.Subsystems;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Network;

public sealed class GlowVoltageReplicationTest
{
    [Fact]
    public void ClientVoltageSnapshotsDriveTheActualLedFactAndRemovalReleasesIt()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        var glow = new SubsystemGlow();
        project.AddSubsystem(glow);
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var ledPoint = new Point3(2, 20, 3);
        var ledData = LedBlock.SetMountingFace(0, 4);
        chunk.SetCellValueFast(ledPoint.X, ledPoint.Y, ledPoint.Z, Terrain.MakeBlockValue(0, 0, ledData));
        var subsystemTerrain = new SubsystemTerrain { Terrain = terrain };
        project.InitializeSubsystem(subsystemTerrain);
        var electricity = new SubsystemElectricity();
        project.InitializeSubsystem(electricity);
        typeof(SubsystemElectricity).GetProperty(nameof(SubsystemElectricity.SubsystemTerrain))!.SetValue(
            electricity,
            subsystemTerrain
        );
        var sourcePoint = new Point3(1, 20, 3);
        var sourceFace = new CellFace(sourcePoint.X, sourcePoint.Y, sourcePoint.Z, 4);
        var source = new SRLatchElectricElement(electricity, sourceFace);
        var led = new LedElectricElement(electricity, new CellFace(ledPoint.X, ledPoint.Y, ledPoint.Z, 4));
        led.OnAdded();
        var fact = Assert.Single(glow.Points);
        Assert.Equal(Color.Transparent, fact.Color);
        source.Connections.Add(
            new ElectricConnection
            {
                ConnectorType = ElectricConnectorType.Output, NeighborConnectorType = ElectricConnectorType.Input,
                NeighborElectricElement = led
            }
        );
        led.Connections.Add(
            new ElectricConnection
            {
                ConnectorType = ElectricConnectorType.Input, NeighborConnectorType = ElectricConnectorType.Output,
                NeighborElectricElement = source
            }
        );
        var faces = (Dictionary<CellFace, ElectricElement>)typeof(SubsystemElectricity).GetField(
            "_electricElementsByCellFace",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!.GetValue(electricity)!;
        faces.Add(sourceFace, source);
        var elements = (Dictionary<ElectricElement, bool>)typeof(SubsystemElectricity).GetField(
            "_electricElements",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!.GetValue(electricity)!;
        elements.Add(led, true);
        electricity.ReceiveNetworkSnapshot(
            new ElectricitySnapshot
                { StartStep = 0, IsBaseline = true, SaveData = new Dictionary<Point3, float> { [sourcePoint] = 1f } }
        );
        electricity.Update(.001f);
        electricity.ReceiveNetworkSnapshot(new ElectricitySnapshot { StartStep = 2 });
        electricity.Update(.001f);
        Assert.Same(fact, Assert.Single(glow.Points));
        Assert.NotEqual(Color.Transparent, fact.Color);
        led.OnRemoved();
        led.OnRemoved();
        Assert.Empty(glow.Points);
    }
}
