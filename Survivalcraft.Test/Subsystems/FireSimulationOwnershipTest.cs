using System.Collections;
using System.Reflection;

using Engine.Core.Mathematics;

using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class FireSimulationOwnershipTest
{
    [Fact]
    public void ReplicaTracksFireWithoutReadingFuelOrRunningSimulation()
    {
        using var project = new RuntimeTestProject(
            RuntimeSide.Client,
            new WorldRuntimeContext(RuntimeSide.Client, false)
        );
        var fire = new SubsystemFireBlockBehavior();
        project.InitializeSubsystem(fire);
        // No terrain or time: replicated fire records must not initialize fuel timers.
        fire.AddFireNet(1, 65, 2, 1f);
        fire.Update(10f);
        Assert.False(fire.SetCellOnFire(1, 65, 2, 1f));
        fire.OnNeighborBlockChanged(1, 65, 2, 2, 65, 2);
        var records = (IDictionary)typeof(SubsystemFireBlockBehavior)
            .GetField("_fireData", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fire)!;
        Assert.Single(records.Keys.Cast<object>());
        fire.RemoveFireNet(1, 65, 2);
        Assert.Empty(records.Keys.Cast<object>());
    }

    [Fact]
    public void AuthorityDoesNotReplayBurnPresentation()
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var node = WorldNetworkBinding.For(project).RequireNode();
        new ComponentOnFirePackageHandler().Handle(
            new BlockBurnEffectPackage { Position = Vector3.Zero, Kind = BlockBurnEffectKind.Burning },
            new PackageReceiveContext(node, node.Runtime, null)
        );
        Assert.Null(project.FindSubsystem<SubsystemFirePresentation>());
    }
}
