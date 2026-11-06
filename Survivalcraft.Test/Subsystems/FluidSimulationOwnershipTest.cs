using System.Collections;
using System.Reflection;

using Game.Blocks;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class FluidSimulationOwnershipTest
{
    [Theory]
    [InlineData(RuntimeSide.Authority)]
    [InlineData(RuntimeSide.Client)]
    public void DerivedSurfaceFlagsDoNotCreateUnpublishedContentVersions(RuntimeSide side)
    {
        using var project = new RuntimeTestProject(side);
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        terrain.SetCellValueFast(1, 65, 1, 18);
        var subsystem = new SubsystemTerrain { Terrain = terrain };
        project.InitializeSubsystem(subsystem);
        var water = new SubsystemWaterBlockBehavior();
        project.InitializeSubsystem(water);
        typeof(SubsystemBlockBehavior).GetProperty("SubsystemTerrain", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(water, subsystem);
        typeof(SubsystemFluidBlockBehavior).GetField("_fluidBlock", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(water, new WaterBlock());
        var revision = chunk.NetworkContentRevision;

        water.UpdateIsTop(18, 1, 65, 1);

        Assert.True(FluidBlock.GetIsTop(Terrain.ExtractData(terrain.GetCellValue(1, 65, 1))));
        Assert.Equal(revision, chunk.NetworkContentRevision);
        var current = terrain.GetCellValue(1, 65, 1);
        terrain.SetCellValueFast(
            1,
            65,
            1,
            Terrain.ReplaceData(current, FluidBlock.SetLevel(Terrain.ExtractData(current), 1))
        );
        Assert.Equal(revision + 1, chunk.NetworkContentRevision);
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
