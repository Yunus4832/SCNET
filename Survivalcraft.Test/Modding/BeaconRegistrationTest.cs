using System.Xml.Linq;

using BeaconMod;

using EntitySystem.TemplatesDatabase;

using Game;
using Game.Blocks;
using Game.ElectricElements;
using Game.Managers;
using Game.Modding;
using Game.Modding.Blocks;
using Game.Modding.Data;
using Game.Network;
using Game.Subsystems;
using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public sealed class BeaconRegistrationTest
{
    [Theory]
    [InlineData("zh-CN", "信标")]
    [InlineData("en-US", "Beacon")]
    [InlineData("pt-PT", "Sinalizador")]
    [InlineData("ru-RU", "Маяк")]
    public void LocalizedBlockTextIsAvailable(string culture, string name)
    {
        ContentManager.Initialize();
        using var runtime = GameModRuntime.Start([Descriptor()]);
        runtime.InitializeLanguage(culture);
        var block = new BeaconBlock();
        Assert.Equal(name, block.GetDisplayName(null, Terrain.MakeBlockValue(300)));
        Assert.False(string.IsNullOrWhiteSpace(block.GetDescription(0)));

    }

    [Fact]
    public void ModRegistersAnElectricalBlockWithoutCustomCommands()
    {
        var host = new ModHost();
        host.LoadAndStart([Descriptor()]);
        try
        {
            var block = Assert.Single(host.Extensions.GetRegistry<BlockRegistration>(BlockExtensions.RegistryName).Entries);
            Assert.Equal(BeaconBlock.Id, block.Key);
            Assert.IsType<BeaconBlock>(block.Value.Factory());
            var contribution = Assert.Single(host.Extensions.GetRegistry<XmlDataRegistration>(XmlDataExtensions.DatabaseRegistryName).Entries);
            var document = XElement.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Database.xml"));
            foreach (var folder in contribution.Value.Read().Elements())
            {
                ModXmlPatcher.Modify(document.Element("DatabaseObjects")!, folder);
            }
            var database = XmlDatabaseSerializer.LoadDatabase(document);
            var member = database.FindDatabaseObject(Guid.Parse("80697f2d-8b78-4c61-9b7c-38da5aaaced1"),
                database.FindDatabaseObjectType("MemberSubsystemTemplate", true), true)!;
            var values = new ValuesDictionary();
            values.PopulateFromDatabaseObject(member);
            Assert.Equal("BeaconMod.BeaconBlockBehavior", values.GetValue<string>("Class"));
            Assert.Equal("Terrain", values.GetValue<string>("Dependencies"));

            Assert.Empty(host.Commands.Definitions);
            var dataContribution = Assert.Single(host.Extensions.GetRegistry<BlockDataRegistration>(BlockExtensions.DataRegistryName).Entries);
            Assert.Contains(";Electrics;ElectricBlockBehavior;", dataContribution.Value.Read());
            var beacon = Assert.IsAssignableFrom<IElectricElementBlock>(block.Value.Factory());
            for (var connector = 0; connector < 4; connector++)
            {
                Assert.Equal(ElectricConnectorType.Input, beacon.GetConnectorType(null!, 0, 4, connector, 0, 0, 0));
                Assert.Null(beacon.GetConnectorType(null!, 0, 0, connector, 0, 0, 0));
            }
            Assert.IsType<BeaconElectricElement>(beacon.CreateElectricElement(new SubsystemElectricity(), 0, 0, 1, 0));
            Assert.Equal(int.MaxValue, beacon.GetConnectionMask(0));
        }
        finally
        {
            host.StopAll();
        }
        Assert.Empty(host.Extensions.GetRegistry<BlockRegistration>(BlockExtensions.RegistryName).Entries);
    }

    [Fact]
    public void ColorChangesPreservePowerAndAllDropsStack()
    {
        ContentManager.Initialize();
        using var runtime = GameModRuntime.Start([Descriptor()]);
        var registry = BlockRuntimeCatalog.Compile(runtime.Host.Extensions.GetRegistry<BlockRegistration>(BlockExtensions.RegistryName));
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
    public void CircuitSimulationUsesHighestInputAndTurnsOffOnDisconnect()
    {
        ContentManager.Initialize();
        var previousRuntime = CurrentModRuntime.Value;
        var previousWorkType = CommonLib.WorkType;
        int? changed = null;
        using var runtime = GameModRuntime.Start([Descriptor(), new ModDescriptor(
            new ModManifest("test.beacon", "Capture", "0.1.0"),
            () => new CaptureChanges(context =>
            {
                changed = context.NewValue;
                context.Cancel = true;
            }))]);
        try
        {
            CurrentModRuntime.Set(runtime);
            CommonLib.WorkType = WorkType.Local;
            using var terrain = new Terrain();
            terrain.AllocateChunk(0, 0);
            var oldValue = Terrain.MakeBlockValue(300, 0, 5);
            terrain.SetCellValueFast(0, 4, 0, oldValue);
            var subsystem = new SubsystemTerrain { Terrain = terrain };
            typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.CellAuthority))!
                .SetValue(subsystem, new TerrainCellAuthority(terrain));
            var electricity = new SubsystemElectricity();
            typeof(SubsystemElectricity).GetProperty(nameof(SubsystemElectricity.SubsystemTerrain))!
                .SetValue(electricity, subsystem);
            var beacon = new BeaconElectricElement(electricity, new CellFace(0, 4, 0, 4));
            foreach (var voltage in new[] { 0.2f, 0.8f })
            {
                beacon.Connections.Add(new ElectricConnection
                {
                    ConnectorType = ElectricConnectorType.Input,
                    NeighborConnectorType = ElectricConnectorType.Output,
                    NeighborElectricElement = new VoltageSource(electricity, voltage)
                });
            }
            beacon.Connections.Add(new ElectricConnection
            {
                ConnectorType = ElectricConnectorType.Input,
                NeighborConnectorType = ElectricConnectorType.Input,
                NeighborElectricElement = new VoltageSource(electricity, 1)
            });
            beacon.Simulate();
            Assert.NotNull(changed);
            Assert.Equal(12, BeaconBlock.GetIntensity(Terrain.ExtractData(changed.Value)));
            Assert.Equal(5, Terrain.ExtractData(changed.Value) & 7);
            // Capture is deliberately vetoed before world side effects; seed its result for the next input state.
            terrain.SetCellValueFast(0, 4, 0, changed.Value);
            changed = null;
            beacon.Connections.Clear();
            beacon.Simulate();
            Assert.Equal(oldValue, changed);
        }
        finally
        {
            CurrentModRuntime.Set(previousRuntime);
            CommonLib.WorkType = previousWorkType;
        }
    }

    private sealed class CaptureChanges(Action<TerrainCellChangingContext> capture) : IMod
    {
        public void Configure(IModContext context) => context.Gameplay.OnTerrainCellChanging(capture);
        public void Start(IModContext context) { }
        public void Stop() { }
    }

    private sealed class VoltageSource(SubsystemElectricity electricity, float voltage)
        : ElectricElement(electricity, new CellFace(1, 4, 0, 4))
    {
        public override float GetOutputVoltage(int face) => voltage;
    }

    [Fact]
    public void DeclaredEmissionEnablesTerrainLightingWhilePowerControlsActualLight()
    {
        var host = new ModHost();
        host.LoadAndStart([Descriptor()]);
        try
        {
            var contribution = Assert.Single(host.Extensions.GetRegistry<BlockDataRegistration>(BlockExtensions.DataRegistryName).Entries);
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

    private static ModDescriptor Descriptor() => new(new ModManifest(ModEntry.ModId, "Beacon", "0.1.0"), () => new ModEntry());
}
