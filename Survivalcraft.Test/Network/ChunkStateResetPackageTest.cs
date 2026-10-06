using System.Reflection;

using Engine.Core;

using Game;
using Game.Network;
using Game.Network.Packages;
using Game.Network.Serialization;
using Game.Subsystems;

namespace Survivalcraft.Test.Network;

public sealed class ChunkStateResetPackageTest
{
    [Fact]
    public void ResetAndAllChunkBaselineRecordsShareOrderedChannel()
    {
        var package = new ChunkStateResetPackage { Chunk = new Point2(-2, 3) };
        using var writer = new PackageStreamWriter();
        package.WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        var clone = new ChunkStateResetPackage();
        clone.ReadData(reader);

        Assert.Equal(package.Chunk, clone.Chunk);
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
        Assert.Equal(PackageTransportPolicy.Bulk, PackageTransportPolicy.Get(clone));
        Assert.Equal(PackageTransportPolicy.Bulk, PackageTransportPolicy.Get(new SignBlockPackage()));
        Assert.Equal(PackageTransportPolicy.Bulk, PackageTransportPolicy.Get(new EditableBlockPackage()));
        Assert.Equal(PackageTransportPolicy.Bulk, PackageTransportPolicy.Get(new ComponentOnFirePackage(1, 2, 3)));
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
        typeof(SubsystemSignBlockBehavior).GetField("_subsystemGameInfo", BindingFlags.Instance | BindingFlags.NonPublic)!
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
