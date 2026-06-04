using System.Runtime.InteropServices;

using Engine.Core;
using Engine.FileStorage;
using Engine.Media;

using Silk.NET.OpenGLES;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using Image = Engine.Media.Image;

namespace Engine.Graphics;

public class Texture2D : GraphicsResource
{
    public GLEnum PixelFormat;

    public GLEnum PixelType;

    internal int texture;

    public Texture2D(int width, int height, int mipLevelsCount, ColorFormat colorFormat)
    {
        InitializeTexture2D(width, height, mipLevelsCount, colorFormat);
        switch (ColorFormat)
        {
            case ColorFormat.Rgba8888:
                PixelFormat = GLEnum.Rgba;
                PixelType = GLEnum.UnsignedByte;
                break;
            case ColorFormat.Rgb565:
                PixelFormat = GLEnum.Rgb;
                PixelType = GLEnum.UnsignedShort565;
                break;
            case ColorFormat.Rgba5551:
                PixelFormat = GLEnum.Rgba;
                PixelType = GLEnum.UnsignedShort5551;
                break;
            case ColorFormat.R8:
#pragma warning disable CS0618 // Type or member is obsolete
                PixelFormat = GLEnum.Luminance;
#pragma warning restore CS0618 // Type or member is obsolete
                PixelType = GLEnum.UnsignedByte;
                break;
            default:
                throw new InvalidOperationException("Unsupported surface format.");
        }

        AllocateTexture();
    }

    public IntPtr NativeHandle => texture;

    public string DebugName
    {
        get => string.Empty;
        set { }
    }

    public int Width { get; set; }

    public int Height { get; set; }

    public ColorFormat ColorFormat { get; set; }

    public int MipLevelsCount { get; set; }

    public object? Tag { get; set; }

    public override void Dispose()
    {
        base.Dispose();
        DeleteTexture();
    }

    public void SetData<T>(int mipLevel, T[] source, int sourceStartIndex = 0) where T : struct
    {
        VerifyParametersSetData(mipLevel, source, sourceStartIndex);
        if (RunMode.Value == RunModeType.HeadlessServer)
        {
            return;
        }

        var gCHandle = GCHandle.Alloc(source, GCHandleType.Pinned);
        try
        {
            var width = MathUtils.Max(Width >> mipLevel, 1);
            var height = MathUtils.Max(Height >> mipLevel, 1);
            var pixels = gCHandle.AddrOfPinnedObject() + sourceStartIndex * Utilities.SizeOf<T>();
            GLWrapper.BindTexture(TextureTarget.Texture2D, (uint)texture, false);
            unsafe
            {
                GLWrapper.GL.TexImage2D(
                    GLEnum.Texture2D,
                    mipLevel,
                    (InternalFormat)PixelFormat,
                    (uint)width,
                    (uint)height,
                    0,
                    PixelFormat,
                    PixelType,
                    pixels.ToPointer()
                );
            }
        }
        finally
        {
            gCHandle.Free();
        }
    }

    public virtual void SetData(Image<Rgba32> source)
    {
        SetData(0, source);
    }

    public virtual unsafe void SetData(int mipLevel, Image<Rgba32> source)
    {
        VerifyParametersSetData(source);
        if (RunMode.Value == RunModeType.HeadlessServer)
        {
            return;
        }

        source.DangerousTryGetSinglePixelMemory(out var memory);
        SetDataInternal(mipLevel, memory.Pin().Pointer);
    }

    public virtual void SetDataInternal(int mipLevel, nint source)
    {
        if (RunMode.Value == RunModeType.HeadlessServer)
        {
            return;
        }

        var width = MathUtils.Max(Width >> mipLevel, 1);
        var height = MathUtils.Max(Height >> mipLevel, 1);
        GLWrapper.BindTexture(TextureTarget.Texture2D, texture, false);
        GLWrapper.GL.TexImage2D(
            TextureTarget.Texture2D,
            mipLevel,
            (InternalFormat)PixelFormat,
            (uint)width,
            (uint)height,
            0,
            PixelFormat,
            PixelType,
            in source
        );
    }

    public virtual unsafe void SetDataInternal(int mipLevel, void* source)
    {
        if (RunMode.Value == RunModeType.HeadlessServer)
        {
            return;
        }

        var width = MathUtils.Max(Width >> mipLevel, 1);
        var height = MathUtils.Max(Height >> mipLevel, 1);
        GLWrapper.BindTexture(TextureTarget.Texture2D, texture, false);
        GLWrapper.GL.TexImage2D(
            TextureTarget.Texture2D,
            mipLevel,
            (InternalFormat)PixelFormat,
            (uint)width,
            (uint)height,
            0,
            PixelFormat,
            PixelType,
            source
        );
    }

    public override void HandleDeviceLost()
    {
        DeleteTexture();
    }

    public override void HandleDeviceReset()
    {
        AllocateTexture();
    }

    public void AllocateTexture()
    {
        if (RunMode.Value == RunModeType.HeadlessServer)
        {
            texture = 0;
            return;
        }

        GLWrapper.GL.GenTextures(1, out uint uTexture);
        texture = (int)uTexture;
        GLWrapper.BindTexture(TextureTarget.Texture2D, uTexture, false);
        for (var i = 0; i < MipLevelsCount; i++)
        {
            var width = MathUtils.Max(Width >> i, 1);
            var height = MathUtils.Max(Height >> i, 1);
            unsafe
            {
                GLWrapper.GL.TexImage2D(
                    TextureTarget.Texture2D,
                    i,
                    (InternalFormat)PixelFormat,
                    (uint)width,
                    (uint)height,
                    0,
                    PixelFormat,
                    PixelType,
                    null
                );
            }
        }
    }

    public void DeleteTexture()
    {
        if (texture == 0)
        {
            return;
        }

        GLWrapper.DeleteTexture(texture);
        texture = 0;
    }

    public override int GetGpuMemoryUsage()
    {
        var num = 0;
        for (var i = 0; i < MipLevelsCount; i++)
        {
            var num2 = MathUtils.Max(Width >> i, 1);
            var num3 = MathUtils.Max(Height >> i, 1);
            num += ColorFormat.GetSize() * num2 * num3;
        }

        return num;
    }

    public static Texture2D Load(LegacyImage image, int mipLevelsCount = 1)
    {
        var texture2D = new Texture2D(image.Width, image.Height, mipLevelsCount, ColorFormat.Rgba8888);
        if (mipLevelsCount > 1)
        {
            var array = LegacyImage.GenerateMipmaps(image, mipLevelsCount).ToArray();
            for (var i = 0; i < array.Length; i++)
            {
                texture2D.SetData(i, array[i].Pixels);
            }
        }
        else
        {
            texture2D.SetData(0, image.Pixels);
        }

        texture2D.Tag = image;
        return texture2D;
    }

    public static Texture2D Load(Image image, int mipLevelsCount = 1)
    {
        var texture2D = new Texture2D(image.Width, image.Height, mipLevelsCount, ColorFormat.Rgba8888);
        texture2D.SetData(image.TrueImage);
        if (RunMode.Value is RunModeType.Gui)
        {
            if (mipLevelsCount > 1)
            {
                GLWrapper.BindTexture(TextureTarget.Texture2D, texture2D.texture, false);
                GLWrapper.GL.GenerateMipmap(TextureTarget.Texture2D);
            }
        }

        texture2D.Tag = image;
        return texture2D;
    }

    public static Texture2D Load(Stream stream, bool premultiplyAlpha = false, int mipLevelsCount = 1)
    {
        var image = Image.Load(stream);
        if (premultiplyAlpha)
        {
            Image.PremultiplyAlpha(image);
        }

        return Load(image, mipLevelsCount);
    }

    public static Texture2D Load(string fileName, bool premultiplyAlpha = false, int mipLevelsCount = 1)
    {
        using var stream = Storage.OpenFile(fileName, OpenFileMode.Read);
        return Load(stream, premultiplyAlpha, mipLevelsCount);
    }

    internal void InitializeTexture2D(int width, int height, int mipLevelsCount, ColorFormat colorFormat)
    {
        Width = width;
        Height = height;
        ColorFormat = colorFormat;
        if (mipLevelsCount > 1)
        {
            var num = 0;
            for (var num2 = MathUtils.Max(width, height); num2 >= 1; num2 /= 2)
            {
                num++;
            }

            MipLevelsCount = MathUtils.Min(num, mipLevelsCount);
        }
        else
        {
            MipLevelsCount = 1;
        }
    }

    public void VerifyParametersSetData<T>(int mipLevel, T[] source, int sourceStartIndex = 0) where T : struct
    {
        if (mipLevel < 0 || mipLevel >= MipLevelsCount)
        {
            throw new ArgumentOutOfRangeException(nameof(mipLevel));
        }

        VerifyNotDisposed();
        var num = Utilities.SizeOf<T>();
        var size = ColorFormat.GetSize();
        var num2 = MathUtils.Max(Width >> mipLevel, 1);
        var num3 = MathUtils.Max(Height >> mipLevel, 1);
        var num4 = size * num2 * num3;

        if (num > size)
        {
            throw new InvalidOperationException("Source array element size is larger than pixel size.");
        }

        if (size % num != 0)
        {
            throw new InvalidOperationException("Pixel size is not an integer multiple of source array element size.");
        }

        if (sourceStartIndex < 0 || (source.Length - sourceStartIndex) * num < num4)
        {
            throw new InvalidOperationException("Not enough data in source array.");
        }
    }

    void VerifyParametersSetData(Image<Rgba32> source)
    {
        VerifyNotDisposed();
        ArgumentNullException.ThrowIfNull(source);
    }
}
