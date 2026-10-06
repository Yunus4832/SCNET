using System.Reflection;

using Engine.Core;

using Game;
using Game.Subsystems;

namespace Survivalcraft.Test.Network;

public sealed class ChunkAttachedStateTest
{
    [Fact]
    public void SignLeaveClearsOnlyTargetChunkAndReentryAcceptsLatestText()
    {
        var behavior = new SubsystemSignBlockBehavior();
        typeof(SubsystemSignBlockBehavior)
            .GetField("_subsystemGameInfo", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(behavior, new SubsystemGameInfo { WorldSettings = new WorldSettings { KeywordBlocking = "" } });
        var departing = new Point3(-1, 70, -16);
        var neighbor = new Point3(0, 70, -16);
        var colors = new[] { Color.White, Color.White, Color.White, Color.White };
        behavior.SetSignData(departing, ["old", "", "", ""], colors, "");
        behavior.SetSignData(neighbor, ["neighbor", "", "", ""], colors, "");

        behavior.ClearChunkState(new Point2(-1, -1));

        Assert.Null(behavior.GetSignData(departing));
        Assert.Equal("neighbor", behavior.GetSignData(neighbor)!.Lines[0]);
        behavior.ClearChunkState(new Point2(-1, -1));
        behavior.SetSignData(departing, ["latest", "", "", ""], colors, "");
        Assert.Equal("latest", behavior.GetSignData(departing)!.Lines[0]);
    }

    [Fact]
    public void MemoryLeaveClearsOnlyTargetChunkAndKeepsGlobalItemDefinitions()
    {
        var behavior = new SubsystemMemoryBankBlockBehavior();
        VerifyEditableState(behavior, new MemoryBankData(), new MemoryBankData());
    }

    [Fact]
    public void TruthTableLeaveClearsOnlyTargetChunkAndKeepsGlobalItemDefinitions()
    {
        var behavior = new SubsystemTruthTableCircuitBlockBehavior();
        VerifyEditableState(behavior, new TruthTableData(), new TruthTableData());
    }

    private static void VerifyEditableState<T>(SubsystemEditableItemBehavior<T> behavior, T oldData, T newData)
        where T : class, IEditableItemData, new()
    {
        var departing = new Point3(-1, 70, -16);
        var neighbor = new Point3(0, 70, -16);
        behavior.SetBlockData(departing, oldData);
        behavior.SetBlockData(neighbor, oldData);
        behavior.ItemsData[7] = oldData;

        behavior.ClearChunkState(new Point2(-1, -1));

        Assert.Null(behavior.GetBlockData(departing));
        Assert.Same(oldData, behavior.GetBlockData(neighbor));
        Assert.Same(oldData, behavior.GetItemData(7));
        behavior.ClearChunkState(new Point2(-1, -1));
        behavior.SetBlockData(departing, newData);
        Assert.Same(newData, behavior.GetBlockData(departing));
    }
}
