using Engine.Graphics;

namespace Engine.Test.Graphics;

[Collection(nameof(ModelResourceCollection))]
public sealed class DepthBufferSnapshotTest
{
    [Theory]
    [InlineData(DepthFormat.Depth16, 2)]
    [InlineData(DepthFormat.Depth24Stencil8, 4)]
    public void SnapshotOwnsItsDepthTextureAndLifecycle(DepthFormat format, int bytesPerPixel)
    {
        var before = GraphicsResource.resources.Count;
        var snapshot = new DepthBufferSnapshot(16, 8, format);
        Assert.Equal(before + 1, GraphicsResource.resources.Count);
        Assert.DoesNotContain(snapshot.Texture, GraphicsResource.resources);
        Assert.Equal(16 * 8 * bytesPerPixel, snapshot.GetGpuMemoryUsage());
        Assert.Equal(format, snapshot.Format);
        snapshot.HandleDeviceLost();
        snapshot.HandleDeviceReset();
        Assert.Equal(before + 1, GraphicsResource.resources.Count);
        Assert.Throws<InvalidOperationException>(() => snapshot.Texture.SetData(0, new byte[16 * 8 * 4]));
        snapshot.Dispose();
        snapshot.Dispose();
        Assert.Equal(before, GraphicsResource.resources.Count);
        Assert.Throws<InvalidOperationException>(snapshot.Capture);
    }

    [Theory]
    [InlineData(0, 8, DepthFormat.Depth16)]
    [InlineData(8, 0, DepthFormat.Depth16)]
    [InlineData(8, 8, DepthFormat.None)]
    public void FailedConstructionDoesNotRegisterResources(int width, int height, DepthFormat format)
    {
        var before = GraphicsResource.resources.Count;
        Assert.Throws<ArgumentOutOfRangeException>(() => new DepthBufferSnapshot(width, height, format));
        Assert.Equal(before, GraphicsResource.resources.Count);
    }
}
