using System.Text;
using System.Text.Json.Nodes;

using Game.ContentReaders;
using Game.Managers;
using Game.Rendering;

namespace Survivalcraft.Test.Content;

public sealed class ClientAssemblyBoundaryTest
{
    [Fact]
    public void ActualContentAndRenderTypesLiveInTheIndependentClientAssembly()
    {
        var assembly = typeof(ContentManager).Assembly;
        Assert.Equal("Survivalcraft.Client", assembly.GetName().Name);
        Assert.Same(assembly, typeof(ContentInfo).Assembly);
        Assert.Same(assembly, typeof(GltfModelReader).Assembly);
        Assert.Same(assembly, typeof(TextureAtlasManager).Assembly);
        Assert.Same(assembly, typeof(InstancedModelsManager).Assembly);
        Assert.Same(assembly, typeof(ModelShader).Assembly);
        Assert.Contains(assembly.GetReferencedAssemblies(), reference => reference.Name == "Survivalcraft.Hosting");
        Assert.DoesNotContain(
            assembly.GetReferencedAssemblies(),
            reference => reference.Name == "Survivalcraft"
        );
    }

    [Fact]
    public void MovedReaderConsumesTheActualPublicContentCarrierWithoutApplicationServices()
    {
        var content = new ContentInfo("example/data.json");
        content.SetContentStream(new MemoryStream(Encoding.UTF8.GetBytes("{\"value\":7}")));
        try
        {
            var value = Assert.IsType<JsonObject>(new JsonObjectReader().Get([content]));
            Assert.Equal(7, value["value"]!.GetValue<int>());
            Assert.Equal(0, content.ContentStream.Position);
        }
        finally { content.Dispose(); }
    }
}
