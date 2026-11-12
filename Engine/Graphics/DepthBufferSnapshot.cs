using Engine.Core.Mathematics;

using Silk.NET.OpenGLES;

namespace Engine.Graphics;

/// <summary>Owns a sampleable copy of one view's opaque scene depth.</summary>
public sealed class DepthBufferSnapshot : GraphicsResource
{
    private uint _framebuffer;

    private readonly DepthFormat _format;

    public Texture2D Texture { get; }

    public DepthFormat Format => _format;

    public DepthBufferSnapshot(int width, int height, DepthFormat format) : base(false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        _format = format;
        Texture = new Texture2D(width, height, format);
        try
        {
            AllocateFramebuffer();
            resources.Add(this);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Capture()
    {
        VerifyNotDisposed();
        if (!Display.IsDeviceAvailable)
        {
            throw new InvalidOperationException("Depth capture requires an active graphics device.");
        }

        var viewport = Display.Viewport;
        if (viewport.Width != Texture.Width || viewport.Height != Texture.Height)
        {
            throw new InvalidOperationException("Depth snapshot dimensions must match the current view.");
        }

        var source = Display.RenderTarget is { } target ? (uint)target.frameBuffer : (uint)GLWrapper.mainFrameBuffer;
        var sourceY = Display.RenderTarget == null
            ? Display.BackbufferSize.Y - viewport.Y - viewport.Height
            : viewport.Y;
        var scissor = GLWrapper.GL.IsEnabled(GLEnum.ScissorTest);
        try
        {
            GLWrapper.BindFramebuffer(_framebuffer);
            GLWrapper.GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, source);
            GLWrapper.GL.Disable(GLEnum.ScissorTest);
            GLWrapper.GL.BlitFramebuffer(
                viewport.X,
                sourceY,
                viewport.X + viewport.Width,
                sourceY + viewport.Height,
                0,
                0,
                Texture.Width,
                Texture.Height,
                ClearBufferMask.DepthBufferBit,
                BlitFramebufferFilter.Nearest
            );
        }
        finally
        {
            GLWrapper.BindFramebuffer(source);
            if (scissor) { GLWrapper.GL.Enable(GLEnum.ScissorTest); }
        }
    }

    public override int GetGpuMemoryUsage() => Texture.GetGpuMemoryUsage();

    public override void HandleDeviceLost()
    {
        DeleteFramebuffer();
        Texture.HandleDeviceLost();
    }

    public override void HandleDeviceReset()
    {
        Texture.HandleDeviceReset();
        AllocateFramebuffer();
    }

    public override void Dispose()
    {
        if (isDisposed) { return; }

        DeleteFramebuffer();
        Texture?.Dispose();
        base.Dispose();
    }

    private void AllocateFramebuffer()
    {
        if (!Display.IsDeviceAvailable) { return; }

        var previous = Display.RenderTarget is { } target ? (uint)target.frameBuffer : (uint)GLWrapper.mainFrameBuffer;
        try
        {
            _framebuffer = GLWrapper.GL.GenFramebuffer();
            if (_framebuffer == 0)
            {
                throw new InvalidOperationException("Cannot allocate depth snapshot framebuffer.");
            }

            GLWrapper.BindFramebuffer(_framebuffer);
            GLWrapper.GL.FramebufferTexture2D(
                GLEnum.Framebuffer,
                _format == DepthFormat.Depth16 ? GLEnum.DepthAttachment : GLEnum.DepthStencilAttachment,
                TextureTarget.Texture2D,
                (uint)Texture.texture,
                0
            );
            unsafe
            {
                var none = GLEnum.None;
                GLWrapper.GL.DrawBuffers(1, &none);
            }

            GLWrapper.GL.ReadBuffer(GLEnum.None);
            var status = GLWrapper.GL.CheckFramebufferStatus(GLEnum.Framebuffer);
            if (status != GLEnum.FramebufferComplete)
            {
                throw new InvalidOperationException($"Cannot create depth snapshot framebuffer ({status}).");
            }
        }
        finally { GLWrapper.BindFramebuffer(previous); }
    }

    private void DeleteFramebuffer()
    {
        if (_framebuffer == 0) { return; }

        GLWrapper.DeleteFramebuffer((int)_framebuffer);
        _framebuffer = 0;
    }
}
