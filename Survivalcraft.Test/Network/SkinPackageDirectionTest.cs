using Game.Network.Packages;
using Game.Network.Packages.Handlers;

namespace Survivalcraft.Test.Network;

public sealed class SkinPackageDirectionTest
{
    [Theory]
    [InlineData("skin.png", true)]
    [InlineData("../skin.png", false)]
    [InlineData("..\\skin.png", false)]
    [InlineData("/tmp/skin.png", false)]
    [InlineData("C:skin.png", false)]
    [InlineData("..", false)]
    [InlineData("", false)]
    public void ResourceNamesCannotEscapeAssetDirectory(string name, bool expected)
    {
        Assert.Equal(expected, ComponentClothingPackageHandler.IsValidResourceName(name));
    }

    [Theory]
    [InlineData(ComponentClothingPackage.DataType.RequestSkin, true, false)]
    [InlineData(ComponentClothingPackage.DataType.WhoHasReply, true, false)]
    [InlineData(ComponentClothingPackage.DataType.ReplySkin, false, true)]
    [InlineData(ComponentClothingPackage.DataType.WhoHas, false, true)]
    public void AssetRequestsAndRepliesHaveExplicitDirections(ComponentClothingPackage.DataType type,
        bool server, bool client)
    {
        Assert.Equal(server, ComponentClothingPackageHandler.AcceptsDirection(type, true));
        Assert.Equal(client, ComponentClothingPackageHandler.AcceptsDirection(type, false));
    }
}
