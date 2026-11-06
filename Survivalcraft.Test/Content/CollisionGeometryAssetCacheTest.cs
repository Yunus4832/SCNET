using Game.Modding;
using Game.Modding.Content;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Content;

[Collection(ConfigFileCollection.Name)]
public sealed class CollisionGeometryAssetCacheTest
{
    [Fact]
    public void WorldDirectoriesShareOnlyImmutableGeometryAndPreserveResourceIdCase()
    {
        var id = ResourceId.Parse("game:Models/IronDoor.glb");
        var asset = BuiltInContentAssets.Load().Single(asset => asset.RelativePath == id.Path);
        var extensions = new ExtensionRegistry();
        extensions.GetRegistry<ContentRegistration>(ContentExtensions.RegistryName)
            .Register(id.Namespace, id, asset);
        var content = ContentCatalog.Compile(extensions);
        var first = content.LoadCollisionGeometry([id]);
        var second = content.LoadCollisionGeometry([id]);
        Assert.NotSame(first, second);
        Assert.Same(first.Get(id), second.Get(id));
        Assert.Throws<InvalidDataException>(() =>
            content.LoadCollisionGeometry([ResourceId.Parse("game:models/irondoor.glb")])
        );
    }
}
