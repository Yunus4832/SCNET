using Game.ElectricElements;
using Game.Subsystems;
using Game.Terrains;
using Game.Terrains.Lighting;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.ElectricElements;

[Collection(ConfigFileCollection.Name)]
public sealed class PhotodiodeLightingTest
{
    [Fact]
    public void TimeChangesUpdateVoltageWithoutSpatialRecalculationAndPendingLightRetainsOutput()
    {
        using var world = new WorldCatalogScope();
        using var terrain = new Terrain();
        var time = new SubsystemTimeOfDay
        {
            SubsystemGameInfo = new SubsystemGameInfo { WorldSettings = new WorldSettings() }
        };
        time.UpdateStarts();
        world.Project.AddSubsystem(time);
        time.SubsystemGameInfo.TimeOfDay = time;
        var subsystem = new SubsystemTerrain { Terrain = terrain, SubsystemGameInfo = time.SubsystemGameInfo };
        world.Project.AddSubsystem(subsystem);
        var electricity = new SubsystemElectricity();
        typeof(SubsystemElectricity).GetProperty(nameof(electricity.SubsystemTerrain))!.SetValue(
            electricity,
            subsystem
        );
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.HasContents = true;
        var request = chunk.DerivedRevisions.Capture();
        Assert.True(chunk.DerivedRevisions.PublishHeights(request));
        Assert.True(chunk.DerivedRevisions.PublishLightSeed(request));
        Assert.True(chunk.DerivedRevisions.PublishLight(request));
        var input = terrain.Lighting.CaptureInput(chunk);
        var snapshot = new TerrainLightSnapshot(new TerrainLightChannels(15, 0));
        Assert.True(terrain.Lighting.TryPublish(chunk, input, snapshot));
        time.TimeOfDayOffset += time.Midday - time.CalculateTimeOfDay();
        var diode = new PhotodiodeElectricElement(electricity, new CellFace(8, 64, 8, 0));
        Assert.Equal(1f, diode.GetOutputVoltage(0));
        time.TimeOfDayOffset += time.Midnight - time.CalculateTimeOfDay();
        Assert.True(diode.Simulate());
        var expected = TerrainLightEnvironment.Capture(time).SkyLightValue / 15f;
        Assert.Equal(expected, diode.GetOutputVoltage(0));
        Assert.Equal(input, terrain.Lighting.CaptureInput(chunk));
        Assert.True(terrain.Lighting.TryGetCurrentSnapshot(chunk, out var same));
        Assert.Same(snapshot, same);
        chunk.DerivedRevisions.InvalidatePropagation();
        Assert.False(diode.Simulate());
        Assert.Equal(expected, diode.GetOutputVoltage(0));
    }
}
