using Game.Network.Handling;

namespace Survivalcraft.Test.Network;

public sealed class PlayerDataPackageDirectionTest
{
    [Theory]
    [InlineData(PlayerDataPackage.DataType.Create, true)]
    [InlineData(PlayerDataPackage.DataType.SetUpdateLocation, true)]
    [InlineData(PlayerDataPackage.DataType.Modify, false)]
    public void PlayerRequestsAndServerResultsHaveExplicitDirections(PlayerDataPackage.DataType type,
        bool acceptedByServer)
    {
        Assert.Equal(acceptedByServer, PlayerDataPackageHandler.AcceptsDirection(type, true));
        Assert.Equal(!acceptedByServer, PlayerDataPackageHandler.AcceptsDirection(type, false));
    }

    [Fact]
    public void UnknownEventIsRejectedInBothDirections()
    {
        var type = (PlayerDataPackage.DataType)255;
        Assert.False(PlayerDataPackageHandler.AcceptsDirection(type, true));
        Assert.False(PlayerDataPackageHandler.AcceptsDirection(type, false));
    }
}
