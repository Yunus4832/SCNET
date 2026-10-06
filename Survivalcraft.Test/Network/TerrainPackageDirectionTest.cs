using Game.Network.Packages;
using Game.Network.Packages.Handlers;

namespace Survivalcraft.Test.Network;

public sealed class TerrainPackageDirectionTest
{
    [Theory]
    [InlineData(SubsystemTerrainPackage.DataType.RequestSyncChunks, true)]
    [InlineData(SubsystemTerrainPackage.DataType.RequestTerrainChunkFragments, true)]
    [InlineData(SubsystemTerrainPackage.DataType.SyncTerrainChunkFragment, false)]
    [InlineData(SubsystemTerrainPackage.DataType.SyncTerrainCellDelta, false)]
    [InlineData(SubsystemTerrainPackage.DataType.ReplyResult, false)]
    public void TerrainMessagesRequireTheirAuthoritativeDirection(
        SubsystemTerrainPackage.DataType type, bool serverReceives)
    {
        Assert.True(SubsystemTerrainPackageHandler.AcceptsDirection(type, serverReceives));
        Assert.False(SubsystemTerrainPackageHandler.AcceptsDirection(type, !serverReceives));
    }

    [Fact]
    public void UnknownMessageIsRejectedOnBothSides()
    {
        var type = (SubsystemTerrainPackage.DataType)byte.MaxValue;
        Assert.False(SubsystemTerrainPackageHandler.AcceptsDirection(type, true));
        Assert.False(SubsystemTerrainPackageHandler.AcceptsDirection(type, false));
    }
}
