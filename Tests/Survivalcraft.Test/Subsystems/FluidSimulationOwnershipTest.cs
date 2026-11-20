using System.Collections;
using System.Reflection;

using EntitySystem.TemplatesDatabase;

using Game.Blocks;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.Terrains.Distribution;
using Game.Terrains.Execution;
using Game.Terrains.Lighting;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class FluidSimulationOwnershipTest
{
    [Fact]
    public void ReplicaFluidDeltasRequestImmediateGeometryRefresh()
    {
        using var scope = new WorldCatalogScope(RuntimeSide.Client);
        using var terrain = new Terrain();
        terrain.AllocateChunk(0, 0);
        var subsystem = new SubsystemTerrain { Terrain = terrain };
        scope.Project.AddSubsystem(subsystem);
        typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.CellAuthority))!
            .SetValue(subsystem, new TerrainCellAuthority(terrain));
        using var scheduler = new TerrainComputationScheduler(
            subsystem,
            new TerrainLightingCalculator(terrain, scope.Catalog, true, _ => true)
        );
        typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.Computations))!.SetValue(subsystem, scheduler);
        var behaviors = new SubsystemBlockBehaviors();
        scope.Project.AddSubsystem(behaviors);
        behaviors.Load(new ValuesDictionary());
        typeof(SubsystemTerrain).GetField("_subsystemBlockBehaviors", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(subsystem, behaviors);
        var revision = subsystem.TerrainInteractionRevision;

        subsystem.ChangeCellNet(1, 65, 1, Terrain.MakeBlockValue(18, FluidBlock.SetIsTop(3, true)));
        Assert.Equal(revision + 1, subsystem.TerrainInteractionRevision);
        subsystem.ChangeCellNet(1, 65, 1, 0);
        Assert.Equal(revision + 2, subsystem.TerrainInteractionRevision);
        subsystem.ChangeCellNet(1, 65, 1, 0);
        Assert.Equal(revision + 2, subsystem.TerrainInteractionRevision);
    }

    [Theory]
    [InlineData(65)]
    [InlineData(255)]
    public void AuthoritySurfaceChangesInvalidateAndVersionContents(int y)
    {
        using var scope = new WorldCatalogScope(RuntimeSide.Authority);
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var subsystem = new SubsystemTerrain { Terrain = terrain };
        scope.Project.AddSubsystem(subsystem);
        scope.Project.AddSubsystem(new SubsystemTime());
        var water = new SubsystemWaterBlockBehavior();
        scope.Project.AddSubsystem(water);
        water.Load(new ValuesDictionary());
        ((WaterBlock)scope.Catalog.Blocks[18]).TheSameFluidsByIndex[18] = true;
        var behaviors = new SubsystemBlockBehaviors();
        scope.Project.AddSubsystem(behaviors);
        behaviors.Load(new ValuesDictionary());
        typeof(SubsystemTerrain).GetField("_subsystemBlockBehaviors", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(subsystem, behaviors);
        typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.CellAuthority))!
            .SetValue(subsystem, new TerrainCellAuthority(terrain));
        using var scheduler = new TerrainComputationScheduler(
            subsystem,
            new TerrainLightingCalculator(terrain, scope.Catalog, false, _ => true)
        );
        typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.Computations))!.SetValue(subsystem, scheduler);
        var value = Terrain.MakeBlockValue(18, FluidBlock.SetLevel(0, 3));
        terrain.SetCellValueFast(1, y, 1, value);
        var revision = chunk.NetworkContentRevision;
        var invalidations = 0;
        scheduler.DerivedInputsInvalidated += _ => invalidations++;

        water.UpdateIsTop(1, y, 1);

        Assert.True(FluidBlock.GetIsTop(Terrain.ExtractData(terrain.GetCellValue(1, y, 1))));
        Assert.Equal(3, FluidBlock.GetLevel(Terrain.ExtractData(terrain.GetCellValue(1, y, 1))));
        Assert.Equal(revision + 1, chunk.NetworkContentRevision);
        Assert.Equal(1, invalidations);
        water.UpdateIsTop(1, y, 1);
        Assert.Equal(revision + 1, chunk.NetworkContentRevision);
        Assert.Equal(1, invalidations);
        if (y < 255)
        {
            terrain.SetCellValueFast(1, y + 1, 1, Terrain.MakeBlockValue(18));
            revision = chunk.NetworkContentRevision;
            water.OnNeighborBlockChanged(1, y, 1, 1, y + 1, 1);
            Assert.False(FluidBlock.GetIsTop(Terrain.ExtractData(terrain.GetCellValue(1, y, 1))));
            Assert.Equal(3, FluidBlock.GetLevel(Terrain.ExtractData(terrain.GetCellValue(1, y, 1))));
            Assert.Equal(revision + 1, chunk.NetworkContentRevision);
            Assert.Equal(2, invalidations);
        }
    }

    [Fact]
    public void ReplicaKeepsAuthoritativeSurfaceFlags()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Client);
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var value = Terrain.MakeBlockValue(18);
        terrain.SetCellValueFast(1, 65, 1, value);
        var subsystem = new SubsystemTerrain { Terrain = terrain };
        project.InitializeSubsystem(subsystem);
        var water = new SubsystemWaterBlockBehavior();
        project.InitializeSubsystem(water);
        typeof(SubsystemBlockBehavior).GetProperty("SubsystemTerrain", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(water, subsystem);
        typeof(SubsystemFluidBlockBehavior).GetField("_fluidBlock", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(water, new WaterBlock());
        var revision = chunk.NetworkContentRevision;

        water.UpdateIsTop(1, 65, 1);

        Assert.Equal(value, terrain.GetCellValue(1, 65, 1));
        Assert.Equal(revision, chunk.NetworkContentRevision);
    }

    [Theory]
    [InlineData(RuntimeSide.Authority, true)]
    [InlineData(RuntimeSide.Client, false)]
    public void OnlyAuthorityRetainsNeighborAndWriteWork(RuntimeSide side, bool queued)
    {
        using var project = new RuntimeTestProject(side);
        var fluid = new SubsystemWaterBlockBehavior();
        project.InitializeSubsystem(fluid);
        fluid.OnNeighborBlockChanged(1, 65, 2, 2, 65, 2);
        fluid.Set(1, 65, 2, 7);
        foreach (var field in new[] { "_toUpdate", "_toSet" })
        {
            var queue = (IDictionary)typeof(SubsystemFluidBlockBehavior)
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fluid)!;
            Assert.Equal(queued, queue.Count > 0);
        }
    }

    [Theory]
    [InlineData(typeof(SubsystemWaterBlockBehavior))]
    [InlineData(typeof(SubsystemMagmaBlockBehavior))]
    public void ReplicaDoesNotQueueOrAdvanceFluidSimulation(Type type)
    {
        using var project = new RuntimeTestProject(
            RuntimeSide.Client,
            new WorldRuntimeContext(RuntimeSide.Client, false)
        );
        var fluid = (SubsystemFluidBlockBehavior)Activator.CreateInstance(type)!;
        project.InitializeSubsystem(fluid);
        // A side-neighbor notification has no presentation derivation to perform.
        fluid.OnNeighborBlockChanged(1, 65, 2, 2, 65, 2);
        fluid.Set(1, 65, 2, 7);
        Assert.False(fluid.OnFluidInteract(0, 1, 65, 2, 7));
        fluid.FlowTo(1, 65, 2, 0);
        fluid.SpreadFluid();
        ((IUpdateable)fluid).Update(.25f);

        foreach (var field in new[] { "_toUpdate", "_toSet" })
        {
            var queue = (IDictionary)typeof(SubsystemFluidBlockBehavior)
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fluid)!;
            Assert.Empty(queue.Keys.Cast<object>());
        }
    }
}
