using Engine.Graphics;

namespace Engine.Test.Graphics;

public class GraphicsPlatformBehaviorTest
{
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

    [Theory]
    [InlineData(100, "#version 100")]
    [InlineData(300, "#version 300 es")]
    [InlineData(320, "#version 320 es")]
    public void ShaderVersionUsesOpenGlEsProfile(int version, string expected)
    {
        Assert.Equal(expected, Shader.CreateOpenGlEsVersionDirective(version));
    }
}
