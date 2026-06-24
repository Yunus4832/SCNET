using System.Runtime.InteropServices;

using Engine.FileStorage;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Pbm;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Qoi;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

using Color = Engine.Core.Color;

namespace Engine.Media;

public class Image
{
    public static IImageFormatConfigurationModule[] ImageSharpModules =
    [
        new BmpConfigurationModule(),
        new GifConfigurationModule(),
        new JpegConfigurationModule(),
        new PbmConfigurationModule(),
        new PngConfigurationModule(),
        new QoiConfigurationModule(),
        new TgaConfigurationModule(),
        new TiffConfigurationModule(),
        new WebpConfigurationModule()
    ];

    public static Configuration DefaultImageSharpConfiguration =
        new(ImageSharpModules) { PreferContiguousImageBuffers = true };

    public static DecoderOptions DefaultImageSharpDecoderOptions =
        new() { Configuration = DefaultImageSharpConfiguration };

    public static readonly JpegEncoder DefaultJpegEncoder =
        new() { Quality = 95, ColorType = JpegEncodingColor.YCbCrRatio420 };

    public static readonly GifEncoder DefaultGifEncoder = new() { ColorTableMode = GifColorTableMode.Local };

    public int Width => TrueImage.Width;

    public int Height => TrueImage.Height;

    private Color[] _pixels = [];

    public bool ShouldUpdatePixelsCache = true;

    public Color[] Pixels
    {
        get
        {
            if (_pixels.Length == 0 || ShouldUpdatePixelsCache)
            {
                _pixels = new Color[Width * Height];
                ProcessPixelRows(
                    accessor =>
                    {
                        var pixelsSpan = _pixels.AsSpan();
                        for (var y = 0; y < accessor.Height; y++)
                        {
                            MemoryMarshal.Cast<Rgba32, Color>(accessor.GetRowSpan(y))
                                .CopyTo(pixelsSpan.Slice(y * Width, Width));
                        }
                    },
                    false
                );
            }

            ShouldUpdatePixelsCache = false;
            return _pixels;
        }
    }

    public readonly Image<Rgba32> TrueImage = new(1,1, new Rgba32());

    public bool IsDisposed;

    public Image()
    {
    }

    public Image(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        TrueImage = image.TrueImage.Clone();
    }

    public Image(Image<Rgba32> image)
    {
        ArgumentNullException.ThrowIfNull(image);
        TrueImage = image;
    }

    public Image(LegacyImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        TrueImage = new Image<Rgba32>(DefaultImageSharpConfiguration, image.Width, image.Height);
        ProcessPixelRows(accessor =>
            {
                var pixels = image.Pixels.AsSpan();
                for (var y = 0; y < accessor.Height; y++)
                {
                    MemoryMarshal.Cast<Color, Rgba32>(pixels.Slice(y * image.Width, image.Height))
                        .CopyTo(accessor.GetRowSpan(y));
                }
            }
        );
    }

    public Image(int width, int height)
    {
        if (width < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        TrueImage = new Image<Rgba32>(DefaultImageSharpConfiguration, width, height);
    }

    public Rgba32 GetPixelFast(int x, int y) => TrueImage[x, y];

    public Color GetPixel(int x, int y) => x < 0 || x >= Width ? throw new ArgumentOutOfRangeException(nameof(x)) :
        y < 0 || y >= Height ? throw new ArgumentOutOfRangeException(nameof(y)) :
        new Color(TrueImage[x, y].PackedValue);

    public Rgba32 SetPixelFast(int x, int y, Rgba32 color) => TrueImage[x, y] = color;

    public void SetPixel(int x, int y, Color color)
    {
        if (x < 0 ||
            x >= Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if (y < 0 ||
            y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        TrueImage[x, y] = new Rgba32(color.PackedValue);
        ShouldUpdatePixelsCache = true;
    }

    public static void PremultiplyAlpha(Image image) => image.ProcessPixels(pixel => pixel.PremultiplyAlpha());

    public static ImageFileFormat DetermineFileFormat(string extension) =>
        Name2EngineImageFormat.TryGetValue(extension.Substring(1).ToLower(), out var format)
            ? format
            : throw new InvalidOperationException("Unsupported image file format.");

    public static ImageFileFormat DetermineFileFormat(Stream stream) =>
        Name2EngineImageFormat.TryGetValue(SixLabors.ImageSharp.Image.DetectFormat(stream).Name.ToLower(),
            out var format)
            ? format
            : throw new InvalidOperationException("Unsupported image file format.");

    public static Image Load(Stream stream, ImageFileFormat format) =>
        Name2EngineImageFormat.TryGetValue(SixLabors.ImageSharp.Image.DetectFormat(stream).Name.ToLower(),
            out var identifiedFormat)
        && identifiedFormat == format
            ? Load(stream)
            : throw new FormatException($"Image format({identifiedFormat}) is not ${format}");

    public static Image Load(string fileName, ImageFileFormat format)
    {
        using var stream = Storage.OpenFile(fileName, OpenFileMode.Read);
        return Load(stream, format);
    }

    public static Image Load(Stream stream) =>
        new(SixLabors.ImageSharp.Image.Load<Rgba32>(DefaultImageSharpDecoderOptions, stream));

    public static Image Load(string fileName)
    {
        using var stream = Storage.OpenFile(fileName, OpenFileMode.Read);
        return Load(stream);
    }

    public static void Save(Image image, Stream stream, ImageFileFormat format, bool saveAlpha, bool sync = false)
    {
        image.FlushPixelsCache();
        switch (format)
        {
            case ImageFileFormat.Bmp:
            {
                BmpEncoder encoder = new()
                    { BitsPerPixel = saveAlpha ? BmpBitsPerPixel.Pixel32 : BmpBitsPerPixel.Pixel24 };
                if (sync)
                {
                    image.TrueImage.SaveAsBmp(stream, encoder);
                }
                else
                {
                    image.TrueImage.SaveAsBmpAsync(stream, encoder);
                }

                break;
            }
            case ImageFileFormat.Png:
            {
                PngEncoder encoder = new()
                {
                    ColorType = saveAlpha ? PngColorType.RgbWithAlpha : PngColorType.Rgb,
                    TransparentColorMode = PngTransparentColorMode.Clear
                };
                if (sync)
                {
                    image.TrueImage.SaveAsPng(stream, encoder);
                }
                else
                {
                    image.TrueImage.SaveAsPngAsync(stream, encoder);
                }

                break;
            }
            case ImageFileFormat.Jpg:
            {
                if (sync)
                {
                    image.TrueImage.SaveAsJpeg(stream, DefaultJpegEncoder);
                }
                else
                {
                    image.TrueImage.SaveAsJpegAsync(stream, DefaultJpegEncoder);
                }

                break;
            }
            case ImageFileFormat.Gif:
                if (sync)
                {
                    image.TrueImage.SaveAsGif(stream, DefaultGifEncoder);
                }
                else
                {
                    image.TrueImage.SaveAsGifAsync(stream, DefaultGifEncoder);
                }

                break;
            case ImageFileFormat.Pbm:
                if (sync)
                {
                    image.TrueImage.SaveAsPbm(stream);
                }
                else
                {
                    image.TrueImage.SaveAsPbmAsync(stream);
                }

                break;
            case ImageFileFormat.Qoi:
            {
                QoiEncoder encoder = new()
                {
                    ColorSpace = QoiColorSpace.SrgbWithLinearAlpha,
                    Channels = saveAlpha ? QoiChannels.Rgba : QoiChannels.Rgb
                };
                if (sync)
                {
                    image.TrueImage.SaveAsQoi(stream, encoder);
                }
                else
                {
                    image.TrueImage.SaveAsQoiAsync(stream, encoder);
                }

                break;
            }
            case ImageFileFormat.Tiff:
            {
                TiffEncoder encoder = new()
                    { BitsPerPixel = saveAlpha ? TiffBitsPerPixel.Bit32 : TiffBitsPerPixel.Bit24 };
                if (sync)
                {
                    image.TrueImage.SaveAsTiff(stream, encoder);
                }
                else
                {
                    image.TrueImage.SaveAsTiffAsync(stream, encoder);
                }

                break;
            }
            case ImageFileFormat.Tga:
            {
                TgaEncoder encoder = new()
                {
                    BitsPerPixel = saveAlpha ? TgaBitsPerPixel.Pixel32 : TgaBitsPerPixel.Pixel24,
                    Compression = TgaCompression.RunLength
                };
                if (sync)
                {
                    image.TrueImage.SaveAsTga(stream, encoder);
                }
                else
                {
                    image.TrueImage.SaveAsTgaAsync(stream, encoder);
                }

                break;
            }
            case ImageFileFormat.WebP:
            {
                WebpEncoder encoder = new()
                {
                    TransparentColorMode =
                        saveAlpha ? WebpTransparentColorMode.Preserve : WebpTransparentColorMode.Clear,
                    FileFormat = WebpFileFormatType.Lossless
                };
                if (sync)
                {
                    image.TrueImage.SaveAsWebp(stream, encoder);
                }
                else
                {
                    image.TrueImage.SaveAsWebpAsync(stream, encoder);
                }

                break;
            }
            default: throw new InvalidOperationException("Unsupported image file format.");
        }
    }

    public static void Save(Image image, string fileName, ImageFileFormat format, bool saveAlpha)
    {
        using var stream = Storage.OpenFile(fileName, OpenFileMode.Create);
        Save(image, stream, format, saveAlpha, sync: true);
    }

    private void FlushPixelsCache()
    {
        if (_pixels.Length != Width * Height || ShouldUpdatePixelsCache)
        {
            return;
        }

        ProcessPixelRows(
            accessor =>
            {
                var pixelsSpan = _pixels.AsSpan();
                for (var y = 0; y < accessor.Height; y++)
                {
                    MemoryMarshal.Cast<Color, Rgba32>(pixelsSpan.Slice(y * Width, Width))
                        .CopyTo(accessor.GetRowSpan(y));
                }
            },
            false
        );
    }

    public void ProcessPixelRows(PixelAccessorAction<Rgba32> accessorAction, bool shouldUpdatePixelsCache = true)
    {
        TrueImage.ProcessPixelRows(accessorAction);
        if (shouldUpdatePixelsCache)
        {
            ShouldUpdatePixelsCache = true;
        }
    }

    public void ProcessPixels(Func<Rgba32, Rgba32> pixelFunc, bool shouldUpdatePixelsCache = true)
    {
        ProcessPixelRows(
            accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    foreach (ref var pixel in accessor.GetRowSpan(y))
                    {
                        pixel = pixelFunc(pixel);
                    }
                }
            },
            shouldUpdatePixelsCache
        );
    }

    public static readonly Dictionary<string, ImageFileFormat> Name2EngineImageFormat = new()
    {
        { "bmp", ImageFileFormat.Bmp },
        { "png", ImageFileFormat.Png },
        { "jpg", ImageFileFormat.Jpg },
        { "jpeg", ImageFileFormat.Jpg },
        { "gif", ImageFileFormat.Gif },
        { "pbm", ImageFileFormat.Pbm },
        { "qoi", ImageFileFormat.Qoi },
        { "tiff", ImageFileFormat.Tiff },
        { "tga", ImageFileFormat.Tga },
        { "webp", ImageFileFormat.WebP }
    };

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        _pixels = null!;
        TrueImage.Dispose();
    }

    public static implicit operator Image(Image<Rgba32> image) => new(image);

    public static implicit operator Image<Rgba32>(Image image) => image.TrueImage;
}
