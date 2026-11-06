using BeaconMod;

using Game.Blocks;
using Game.ElectricElements;
using Game.Modding;
using Game.Modding.Blocks;
using Game.Modding.Data;
using Game.Terrains;


namespace BeaconMod.Test;

public sealed class BeaconLogicTest
{
    [Fact]
    public void ColorChangesPreservePowerAndAllDropsStack()
    {
        var registry = BlockRuntimeCatalog.Compile(
            [
                new KeyValuePair<ResourceId, BlockRegistration>(
                    ResourceId.Parse("game:air"),
                    new(() => new AirBlock())
                ),
                new KeyValuePair<ResourceId, BlockRegistration>(BeaconBlock.Id, new(() => new BeaconBlock()))
            ]
        );
        var block = (BeaconBlock)registry.ById[BeaconBlock.Id].Block;
        for (var intensity = 0; intensity <= 15; intensity++)
        {
            for (var color = 0; color < 8; color++)
            {
                var data = BeaconBlock.SetIntensity(color, intensity);
                var cycled = BeaconBlock.CycleColor(data);
                Assert.Equal(intensity, BeaconBlock.GetIntensity(cycled));
                Assert.Equal((color + 1) % 8, cycled & 7);
                Assert.Equal(intensity, block.GetEmittedLightAmount(Terrain.MakeBlockValue(300, 0, data)));
                var drops = new List<BlockDropValue>();
                block.GetDropValues(null!, Terrain.MakeBlockValue(300, 15, data), 0, 0, drops, out _);
                Assert.Equal(Assert.Single(block.GetCreativeValues()), Assert.Single(drops).Value);
                Assert.False(block.IsEditable(0));
            }
        }

        foreach (var entry in registry.ById.Values)
        {
            entry.Block.Dispose();
        }
    }

    [Fact]
    public void VoltageProducesMonotonicBrightnessAndZeroPowerTurnsOff()
    {
        var previous = -1;
        for (var step = 0; step <= 15; step++)
        {
            var intensity = BeaconElectricElement.IntensityFromVoltage(step / 15f);
            Assert.Equal(step, intensity);
            Assert.True(intensity > previous);
            Assert.Equal(step > 0, BeaconBlock.IsEnabled(BeaconBlock.SetIntensity(4, intensity)));
            previous = intensity;
        }

        Assert.Equal(0, BeaconElectricElement.IntensityFromVoltage(-1));
        Assert.Equal(15, BeaconElectricElement.IntensityFromVoltage(2));
    }

    [Fact]
    public void DeclaredEmissionEnablesTerrainLightingWhilePowerControlsActualLight()
    {
        var host = new ModHost();
        host.LoadAndStart([Descriptor()]);
        try
        {
            var contribution = Assert.Single(
                host.Extensions
                    .GetRegistry<BlockDataRegistration>(BlockExtensions.DataRegistryName).Entries
            );
            var lines = contribution.Value.Read().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var fields = lines[0].Split(';');
            var values = lines[1].Split(';');
            var declared = int.Parse(values[Array.IndexOf(fields, "EmittedLightAmount")]);
            var block = new BeaconBlock { EmittedLightAmount = declared };
            // TerrainUpdater uses this static marker before calling the dynamic light method.
            Assert.True(block.EmittedLightAmount > 0);
            for (var intensity = 0; intensity <= 15; intensity++)
            {
                var value = Terrain.MakeBlockValue(300, 0, BeaconBlock.SetIntensity(2, intensity));
                Assert.Equal(intensity, block.GetEmittedLightAmount(value));
                Assert.True(block.GetEmittedLightAmount(value) <= block.EmittedLightAmount);
            }
        }
        finally
        {
            host.StopAll();
        }
    }

    private static ModDescriptor Descriptor() =>
        new(new ModManifest(ModEntry.ModId, "Beacon", "0.1.0"), () => new ModEntry());
}
