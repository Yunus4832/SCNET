using System.Reflection;

using Engine.Core.Mathematics;

using Game.ElectricElements;
using Game.Network.Transport;
using Game.Subsystems;
using Game.World;

namespace Survivalcraft.Test.Network;

public sealed class ChunkStateResetPackageTest
{
    [Fact]
    public void ResetAndAllChunkBaselineRecordsShareOrderedChannel()
    {
        var package = new ChunkStateResetPackage(new Point2(-2, 3));
        var writer = ProtocolMessageCodec.Encode(package);
        var reader = writer;
        var clone = (ChunkStateResetPackage)ProtocolMessageCodec.Decode(reader);

        Assert.Equal(package.Chunk, clone.Chunk);

        Assert.Equal(PackageTransportPolicy.Bulk, PackageTransportPolicy.Get(clone));
        Assert.Equal(PackageTransportPolicy.Bulk, PackageTransportPolicy.Get(new SignBlockPackage()));
        Assert.Equal(
            PackageTransportPolicy.Bulk,
            PackageTransportPolicy.Get(
                new EditableBlockValuePackage
                    { Target = new EditableWorldBlockTarget(default), ItemType = BuiltInBlockEditKind.Button }
            )
        );
        Assert.Equal(
            PackageTransportPolicy.Bulk,
            PackageTransportPolicy.Get(new BlockFireRemovedPackage { X = 1, Y = 2, Z = 3 })
        );
    }

    [Fact]
    public void ResetRemovesOnlyTargetChunkBlockDataAndKeepsItemDefinitions()
    {
        var behavior = new SubsystemMemoryBankBlockBehavior();
        var stale = new Point3(-1, 10, 16);
        var other = new Point3(0, 10, 16);
        var data = new MemoryBankData();
        behavior.SetBlockData(stale, data);
        behavior.SetBlockData(other, data);
        behavior.ItemsData[7] = data;

        behavior.ClearChunkState(new Point2(-1, 1));

        Assert.Null(behavior.GetBlockData(stale));
        Assert.Same(data, behavior.GetBlockData(other));
        Assert.Same(data, behavior.GetItemData(7));
    }

    [Fact]
    public void ResetRemovesStaleSignButNotNeighboringChunk()
    {
        var behavior = new SubsystemSignBlockBehavior();
        typeof(SubsystemSignBlockBehavior).GetField(
                "_subsystemGameInfo",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!
            .SetValue(behavior, new SubsystemGameInfo { WorldSettings = new WorldSettings { KeywordBlocking = "" } });
        var stale = new Point3(15, 10, 15);
        var other = new Point3(16, 10, 15);
        string[] lines = ["one", "two", "three", "four"];
        Color[] colors = [Color.White, Color.White, Color.White, Color.White];
        behavior.SetSignData(stale, lines, colors, "");
        behavior.SetSignData(other, lines, colors, "");

        behavior.ClearChunkState(new Point2(0, 0));

        Assert.Null(behavior.GetSignData(stale));
        Assert.NotNull(behavior.GetSignData(other));
    }
}
