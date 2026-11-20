using System.Collections;
using System.Reflection;

using Engine.Core.Mathematics;

using Game.Blocks;
using Game.Modding.Blocks;
using Game.Network;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class FireBlockLifecycleTest
{
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
