using EntitySystem.Core;

using Game.Content;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class SkinPackageDirectionTest
{
    [Fact]
    public void MissingSkinQueriesDeduplicateWithinOneRuntimeButNotAcrossWorlds()
    {
        using var firstProject = new Project();
        using var secondProject = new Project();
        var first = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var second = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var directory = Path.Combine(Path.GetTempPath(), "scnet-skin-direction-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Engine.Core.FileStorage.Storage.RegisterFileSystemRoot("skin-direction", directory);
        var store = new CharacterSkinStore("skin-direction:/");
        var builder = new PackageHandlerCatalogBuilder();
        builder.RegisterFamily(() => new ComponentClothingPackageHandler(store));
        var catalog = builder.Build();
        WorldNetworkBinding.For(first.Runtime).InstallHandlers(catalog);
        WorldNetworkBinding.For(second.Runtime).InstallHandlers(catalog);
        first.Runtime.BindProject(firstProject);
        second.Runtime.BindProject(secondProject);
        var request = new SkinRequestPackage { SkinName = $"missing-{Guid.NewGuid():N}" };
        try
        {
            Query(first);
            Query(first);
            Assert.Equal(1, first.PendingPackageCount);
            Query(second);
            Assert.Equal(1, second.PendingPackageCount);
        }
        finally
        {
            first.Runtime.Close();
            second.Runtime.Close();
            Directory.Delete(directory, true);
        }

        void Query(NetNode node)
        {
            var sender = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
            WorldNetworkBinding.For(node.Runtime).Dispatch(
                request,
                new PackageReceiveContext(node, node.Runtime, sender)
            );
        }
    }

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
    public void AssetRequestsAndRepliesHaveExplicitDirections(
        ComponentClothingPackage.DataType type,
        bool server,
        bool client
    )
    {
        Assert.Equal(server, ComponentClothingPackageHandler.AcceptsDirection(type, true));
        Assert.Equal(client, ComponentClothingPackageHandler.AcceptsDirection(type, false));
    }
}
