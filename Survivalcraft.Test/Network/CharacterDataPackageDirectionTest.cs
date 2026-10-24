using Game.Network.Handling;

namespace Survivalcraft.Test.Network;

public sealed class CharacterDataPackageDirectionTest
{
    [Theory]
    [InlineData(CharacterDataPackage.DataType.Create, true)]
    [InlineData(CharacterDataPackage.DataType.SetUpdateLocation, true)]
    [InlineData(CharacterDataPackage.DataType.Modify, false)]
    [InlineData(CharacterDataPackage.DataType.CreationRejected, false)]
    public void PlayerRequestsAndServerResultsHaveExplicitDirections(
        CharacterDataPackage.DataType type,
        bool acceptedByServer
    )
    {
        Assert.Equal(acceptedByServer, CharacterDataPackageHandler.AcceptsDirection(type, true));
        Assert.Equal(!acceptedByServer, CharacterDataPackageHandler.AcceptsDirection(type, false));
    }

    [Fact]
    public void UnknownEventIsRejectedInBothDirections()
    {
        var type = (CharacterDataPackage.DataType)255;
        Assert.False(CharacterDataPackageHandler.AcceptsDirection(type, true));
        Assert.False(CharacterDataPackageHandler.AcceptsDirection(type, false));
    }
}
