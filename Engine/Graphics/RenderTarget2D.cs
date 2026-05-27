using Engine.Core;
using Engine.Media;

using Silk.NET.OpenGLES;

namespace Engine.Graphics;

public sealed class RenderTarget2D : Texture2D
{
    internal int depthBuffer;

    internal int frameBuffer;

    public RenderTarget2D(
        int width,
        int height,
        int mipLevelsCount,
        ColorFormat colorFormat,
        DepthFormat depthFormat
    ) : base(width, height, mipLevelsCount, colorFormat)
    {
        try
        {
            InitializeRenderTarget2D(width, height, mipLevelsCount, colorFormat, depthFormat);
            AllocateRenderTarget();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public DepthFormat DepthFormat { get; set; }

    public override void Dispose()
    {
        base.Dispose();
        DeleteRenderTarget();
    }

    public void GetData<T>(T[] target, int targetStartIndex, Rectangle sourceRectangle) where T : unmanaged
    {
        VerifyParametersGetData(target, targetStartIndex, sourceRectangle);
        GLWrapper.BindFramebuffer((uint)frameBuffer);
        var targetSpan = target.AsSpan(targetStartIndex);
        GLWrapper.GL.ReadPixels(
            sourceRectangle.Left,
            sourceRectangle.Top,
            (uint)sourceRectangle.Width,
            (uint)sourceRectangle.Height,
            GLEnum.Rgba,
            GLEnum.UnsignedByte,
            targetSpan
        );
    }

    public void GenerateMipMaps()
    {
        GLWrapper.BindTexture(TextureTarget.Texture2D, (uint)texture, false);
        GLWrapper.GL.GenerateMipmap(TextureTarget.Texture2D);
    }

    public override void HandleDeviceLost()
    {
        DeleteRenderTarget();
    }

    public override void HandleDeviceReset()
    {
        AllocateRenderTarget();
    }

    public void AllocateRenderTarget()
    {
        GLWrapper.GL.GenFramebuffers(1, out uint uFrameBuffer);
        frameBuffer = (int)uFrameBuffer;
        GLWrapper.BindFramebuffer(uFrameBuffer);
        GLWrapper.GL.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0, TextureTarget.Texture2D,
            (uint)texture, 0);
        if (DepthFormat != 0)
        {
            GLWrapper.GL.GenRenderbuffers(1, out uint uDepthBuffer);
            depthBuffer = (int)uDepthBuffer;
            GLWrapper.GL.BindRenderbuffer(GLEnum.Renderbuffer, uDepthBuffer);
            GLWrapper.GL.RenderbufferStorage(GLEnum.Renderbuffer, GLWrapper.TranslateDepthFormat(DepthFormat),
                (uint)Width, (uint)Height);
            GLWrapper.GL.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthAttachment, GLEnum.Renderbuffer,
                uDepthBuffer);
            GLWrapper.GL.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.StencilAttachment, GLEnum.Renderbuffer, 0);
        }
        else
        {
            GLWrapper.GL.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthAttachment, GLEnum.Renderbuffer, 0);
            GLWrapper.GL.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.StencilAttachment, GLEnum.Renderbuffer, 0);
        }

        var framebufferErrorCode = GLWrapper.GL.CheckFramebufferStatus(GLEnum.Framebuffer);
        if (framebufferErrorCode != GLEnum.FramebufferComplete)
        {
            throw new InvalidOperationException($"Error creating framebuffer ({framebufferErrorCode.ToString()}).");
        }
    }

    public void DeleteRenderTarget()
    {
        if (depthBuffer != 0)
        {
            var uDepthBuffer = (uint)depthBuffer;
            GLWrapper.GL.DeleteRenderbuffers(1, ref uDepthBuffer);
            depthBuffer = 0;
        }

        if (frameBuffer != 0)
        {
            GLWrapper.DeleteFramebuffer(frameBuffer);
            frameBuffer = 0;
        }
    }

    public static void Save(RenderTarget2D renderTarget, Stream stream, ImageFileFormat format, bool saveAlpha)
    {
        if (renderTarget.ColorFormat != 0)
        {
            throw new InvalidOperationException("Unsupported color format.");
        }

        var image = new Image(renderTarget.Width, renderTarget.Height);
        renderTarget.GetData(image.Pixels, 0, new Rectangle(0, 0, renderTarget.Width, renderTarget.Height));
        Image.Save(image, stream, format, saveAlpha);
    }

    public override int GetGpuMemoryUsage()
    {
        return base.GetGpuMemoryUsage() + DepthFormat.GetSize() * Width * Height;
    }

    public void InitializeRenderTarget2D(int width, int height, int mipLevelsCount, ColorFormat colorFormat,
        DepthFormat depthFormat)
    {
        DepthFormat = depthFormat;
    }

    public void VerifyParametersGetData<T>(T[] target, int targetStartIndex, Rectangle sourceRectangle) where T : struct
    {
        VerifyNotDisposed();
        var size = ColorFormat.GetSize();
        var num = Utilities.SizeOf<T>();
        if (num > size)
        {
            throw new InvalidOperationException("Target array element size can not greater than Pixel size");
        }

        if (size % num != 0)
        {
            throw new InvalidOperationException("Pixel size is not an integer multiple of target array element size.");
        }

        if (sourceRectangle.Left < 0 || sourceRectangle.Width <= 0 || sourceRectangle.Top < 0 ||
            sourceRectangle.Height <= 0 || sourceRectangle.Left + sourceRectangle.Width > Width ||
            sourceRectangle.Top + sourceRectangle.Height > Height)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRectangle));
        }

        if (targetStartIndex < 0 || targetStartIndex >= target.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(targetStartIndex));
        }

        if ((target.Length - targetStartIndex) * num < sourceRectangle.Width * sourceRectangle.Height * size)
        {
            throw new InvalidOperationException("Not enough space in target array.");
        }
    }
}
