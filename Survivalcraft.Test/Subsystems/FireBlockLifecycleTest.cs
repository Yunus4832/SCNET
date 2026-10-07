using System.Collections;
using System.Reflection;

using Engine.Core;

using Game;
using Game.Blocks;
using Game.Managers;
using Game.Network;
using Game.Network.Enums;
using Game.Subsystems;
using Game.Terrains;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class FireBlockLifecycleTest
{
    [Fact]
    public void ClientTerrainRemovalAndChunkDiscardClearOnlyTheirFireRecords()
    {
        var mode = CommonLib.WorkType;
        var air = BlocksManager.Blocks[0];
        CommonLib.WorkType = WorkType.Client;
        BlocksManager.Blocks[0] = new AirBlock();
        try
        {
            using var terrain = new Terrain();
            var chunk = terrain.AllocateChunk(0, 0);
            terrain.AllocateChunk(2, 0);
            var subsystem = new SubsystemFireBlockBehavior();
            typeof(SubsystemBlockBehavior).GetProperty("SubsystemTerrain",
                BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(subsystem, new SubsystemTerrain { Terrain = terrain });
            var records = (IDictionary)typeof(SubsystemFireBlockBehavior)
                .GetField("_fireData", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(subsystem)!;

            subsystem.AddFireNet(1, 10, 1, 1f);
            subsystem.AddFireNet(2, 10, 1, 1f);
            subsystem.AddFireNet(33, 10, 1, 1f);
            Assert.Equal(3, records.Count);

            subsystem.OnBlockRemoved(FireBlock.Index, 0, 1, 10, 1);
            Assert.Equal(2, records.Count);
            subsystem.OnChunkDiscarding(chunk);
            Assert.Single(records.Keys.Cast<object>());
            subsystem.ClearChunkState(new Point2(2, 0));
            Assert.Empty(records.Keys.Cast<object>());
        }
        finally
        {
            CommonLib.WorkType = mode;
            BlocksManager.Blocks[0] = air;
        }
    }
}
