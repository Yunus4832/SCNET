using Engine.Core.Mathematics;
using Engine.Graphics;

namespace Engine.Test.Graphics;

public class GraphicsPlatformBehaviorTest
{
    [Fact]
    public void TexturedQuadsKeepFullWidthIndicesAfter65536Vertices()
    {
        var batch = new TexturedBatch3D();
        batch.TriangleVertices.Count = 65536;
        batch.QueueQuad(
            Vector3.Zero,
            Vector3.UnitX,
            Vector3.One,
            Vector3.UnitZ,
            Vector2.Zero,
            Vector2.UnitX,
            Vector2.One,
            Vector2.UnitY,
            Color.White
        );

        Assert.Equal([65536, 65537, 65538, 65538, 65539, 65536], batch.TriangleIndices.ToArray());
    }

    [Fact]
    public void TwoDimensionalBatchesHaveConsistentAlphaBlendDefaults()
    {
        var flatBatch = new FlatBatch2D();
        var texturedBatch = new TexturedBatch2D();

        Assert.Same(DepthStencilState.None, flatBatch.DepthStencilState);
        Assert.Same(RasterizerState.CullNoneScissor, flatBatch.RasterizerState);
        Assert.Same(BlendState.AlphaBlend, flatBatch.BlendState);
        Assert.Same(DepthStencilState.None, texturedBatch.DepthStencilState);
        Assert.Same(RasterizerState.CullNoneScissor, texturedBatch.RasterizerState);
        Assert.Same(BlendState.AlphaBlend, texturedBatch.BlendState);
        Assert.Same(SamplerState.LinearClamp, texturedBatch.SamplerState);
    }

    [Fact]
    public void DepthOnlyStatePreservesColorAndIsImmutable()
    {
        Assert.True(BlendState.Opaque.ColorWriteEnabled);
        Assert.False(BlendState.DepthOnly.ColorWriteEnabled);
        Assert.Throws<InvalidOperationException>(() => BlendState.DepthOnly.ColorWriteEnabled = true);
    }

    [Theory]
    [InlineData(100, "#version 100")]
    [InlineData(300, "#version 300 es")]
    [InlineData(320, "#version 320 es")]
    public void ShaderVersionUsesOpenGlEsProfile(int version, string expected)
    {
        Assert.Equal(expected, Shader.CreateOpenGlEsVersionDirective(version));
    }
}
