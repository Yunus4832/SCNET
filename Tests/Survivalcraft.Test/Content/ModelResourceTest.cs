using System.Text;

using Engine.Media.Models;

using Game.Managers;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Content;

[Collection(ConfigFileCollection.Name)]
public sealed class ModelResourceTest
{
    [Fact]
    public void GltfDependenciesResolveRelativeToPackagedModelIncludingEscapedNames()
    {
        AssetManager.Initialize();
        try
        {
            var resource = new AssetInfo("example.models/shared mesh.bin");
            resource.SetAssetStream(new MemoryStream(BitConverter.GetBytes(1f)));
            AssetManager.Add(resource);
            var owner = new AssetInfo("example.models/models/model.gltf");
            using var stream = new MemoryStream(
                Encoding.UTF8.GetBytes(
                    """
                    {"asset":{"version":"2.0"},"buffers":[{"uri":"../shared%20mesh.bin","byteLength":4}],
                     "bufferViews":[{"buffer":0,"byteLength":4}],"accessors":[{"bufferView":0,"componentType":5126,"count":1,"type":"SCALAR"}]}
                    """
                )
            );
            var model = ModelData.Load(stream, uri => AssetManager.OpenRelativeResource(owner, uri));
            Assert.Equal(1, Assert.Single(model.Accessors).GetComponent(0, 0));
        }
        finally
        {
            AssetManager.Initialize();
        }
    }
}
