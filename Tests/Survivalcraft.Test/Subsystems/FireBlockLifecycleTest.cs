using System.Collections;
using System.Reflection;

using Engine.Core.Mathematics;

using Game.Blocks;
using Game.Modding.Blocks;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Replication;
using Game.Network.Transport;
using Game.Protocol.Connection;
using Game.Protocol.Entities;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class FireBlockLifecycleTest
{
    [Fact]
    public void AuthorityNotifiesObserversOnlyWhenFireMembershipChanges()
    {
        var node = new NetNode(NetNodeOptions.Default, false, PackageHandlerCatalog.Empty, NodeRole.Authority);
        using var scope = new WorldCatalogScope(node);
        using var terrain = new Terrain();
        terrain.AllocateChunk(0, 0);
        var fire = new SubsystemFireBlockBehavior();
        var interest = new SubsystemNetworkInterest();
        scope.Project.AddSubsystem(interest);
        scope.Project.InitializeSubsystem(fire);
        typeof(SubsystemBlockBehavior).GetProperty("SubsystemTerrain", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(fire, new SubsystemTerrain { Terrain = terrain });
        var observer = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid())
            { State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live };
        node.AddClient(observer);
        using var replica = new RuntimeTestProject(RuntimeSide.Client);
        var link = InProcessMessageConnection.CreatePair(
            node,
            observer,
            WorldNetworkBinding.For(replica).RequireNode(),
            new Client(Guid.Empty)
        );
        using var toClient = link.ToClient;
        using var toAuthority = link.ToAuthority;
        var spatial = (SpatialInterestIndex)typeof(SubsystemNetworkInterest)
            .GetField("_spatial", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(interest)!;
        spatial.SetLocation(observer, Vector2.Zero, 32f);

        var fireValue = BuiltInBlockRegistry.GetIndex<FireBlock>();
        fire.OnBlockAdded(fireValue, 0, 1, 10, 1);
        fire.OnBlockAdded(fireValue, 0, 1, 10, 1);

        var pending = (PendingLogicalMessages)typeof(NetNode)
            .GetField("_pendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(node)!;
        var notification = Assert.Single(pending, entry => entry.Package is BlockFireAddedPackage);
        Assert.Same(observer, notification.Audience.SingleRecipient);
        Assert.Single(fire.FirePoints);

        fire.OnBlockRemoved(fireValue, 0, 1, 10, 1);
        fire.OnBlockRemoved(fireValue, 0, 1, 10, 1);

        var removal = Assert.Single(pending, entry => entry.Package is BlockFireRemovedPackage);
        Assert.Same(observer, removal.Audience.SingleRecipient);
        Assert.Empty(fire.FirePoints);
    }

    [Fact]
    public void ClientTerrainRemovalAndChunkDiscardClearOnlyTheirFireRecords()
    {
        var mode = CommonLib.Net.Role;
        using var scope = new WorldCatalogScope(RuntimeSide.Client);
        CommonLib.Net.Role = NodeRole.Client;

        try
        {
            using var terrain = new Terrain();
            var chunk = terrain.AllocateChunk(0, 0);
            terrain.AllocateChunk(2, 0);
            var subsystem = new SubsystemFireBlockBehavior();
            scope.Project.InitializeSubsystem(subsystem);
            typeof(SubsystemBlockBehavior).GetProperty(
                    "SubsystemTerrain",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )!
                .SetValue(subsystem, new SubsystemTerrain { Terrain = terrain });
            var records = (IDictionary)typeof(SubsystemFireBlockBehavior)
                .GetField("_fireData", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(subsystem)!;

            subsystem.AddFireNet(1, 10, 1, 1f);
            subsystem.AddFireNet(2, 10, 1, 1f);
            subsystem.AddFireNet(33, 10, 1, 1f);
            subsystem.AddFireNet(33, 10, 1, 1f);
            Assert.Equal(3, records.Count);
            Assert.Equal(3, subsystem.FirePoints.Count);
            Assert.True(subsystem.ContainsFire(new Point3(33, 10, 1)));

            subsystem.OnBlockRemoved(BuiltInBlockRegistry.GetIndex<FireBlock>(), 0, 1, 10, 1);
            Assert.Equal(2, records.Count);
            subsystem.OnChunkDiscarding(chunk);
            Assert.Single(records.Keys.Cast<object>());
            subsystem.ClearChunkState(new Point2(2, 0));
            Assert.Empty(records.Keys.Cast<object>());
            Assert.Empty(subsystem.FirePoints);
            subsystem.AddFireNet(33, 10, 1, 1f);
            Assert.Equal(new Point3(33, 10, 1), Assert.Single(subsystem.FirePoints));
            subsystem.ClearChunkState(new Point2(2, 0));
            Assert.Empty(subsystem.FirePoints);
        }
        finally
        {
            CommonLib.Net.Role = mode;
        }
    }
}
