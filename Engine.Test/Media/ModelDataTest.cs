using Engine.Media;

namespace Engine.Test.Media;

public class ModelDataTest
{
    [Fact]
    public void ColladaLoadsModelAndVertexDataWithoutGraphicsInitialization()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "TestAssets", "StoneClub.dae"));
        var model = ModelData.Load(stream);

        Assert.NotEmpty(model.Bones);
        Assert.NotEmpty(model.Meshes);
        Assert.NotEmpty(model.Buffers);
        foreach (var buffer in model.Buffers)
        {
            Assert.NotEmpty(buffer.Vertices);
            Assert.NotEmpty(buffer.Indices);
            Assert.True(buffer.VertexDeclaration.VertexStride > 0);
            Assert.Equal(0, buffer.Vertices.Length % buffer.VertexDeclaration.VertexStride);
            Assert.Contains(buffer.VertexDeclaration.VertexElements, element => element.SemanticName == "POSITION");
        }
    }
}
