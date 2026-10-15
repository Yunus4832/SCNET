using System.Reflection;
using System.Runtime.InteropServices;

namespace Engine.Media.Native;

internal static unsafe class CodecNative
{
    static CodecNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(CodecNative).Assembly, Resolve);
    }

    private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        var file = name switch
        {
            "scnet_png" => OperatingSystem.IsWindows() ? "libpng16.dll" : "libpng.so",
            "scnet_webp" => OperatingSystem.IsWindows() ? "libwebp.dll" : "libwebp.so",
            "scnet_jpeg" => OperatingSystem.IsWindows() ? "turbojpeg.dll" : "libturbojpeg.so",
            _ => null
        };
        if (file is null)
        {
            return 0;
        }
        var platform = OperatingSystem.IsAndroid() ? "android" : OperatingSystem.IsWindows() ? "win" : "linux";
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm => "arm",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.X64 => "x64",
            _ => throw new PlatformNotSupportedException("Unsupported image codec architecture.")
        };
        var nativePath = File.Exists(Path.Combine(AppContext.BaseDirectory, file))
            ? AppContext.BaseDirectory
            : Path.Combine(AppContext.BaseDirectory, "runtimes", $"{platform}-{architecture}", "native");
        if (name == "scnet_webp" && !OperatingSystem.IsAndroid())
        {
            var dependency = OperatingSystem.IsWindows() ? "libsharpyuv.dll" : "libsharpyuv.so";
            NativeLibrary.Load(File.Exists(Path.Combine(nativePath, dependency))
                ? Path.Combine(nativePath, dependency)
                : dependency, assembly, searchPath);
        }
        return File.Exists(Path.Combine(nativePath, file))
            ? NativeLibrary.Load(Path.Combine(nativePath, file))
            : NativeLibrary.Load(file, assembly, searchPath);
    }

    [DllImport("scnet_png", CallingConvention = CallingConvention.Cdecl, EntryPoint = "png_image_begin_read_from_memory")]
    internal static extern int PngBeginRead(PngImage* image, byte* input, nuint size);

    [DllImport("scnet_png", CallingConvention = CallingConvention.Cdecl, EntryPoint = "png_image_finish_read")]
    internal static extern int PngFinishRead(PngImage* image, void* background, byte* output,
        int stride, void* colormap);

    [DllImport("scnet_png", CallingConvention = CallingConvention.Cdecl, EntryPoint = "png_image_write_to_memory")]
    internal static extern int PngWrite(PngImage* image, byte* output, nuint* size, int linear,
        byte* pixels, int stride, void* colormap);

    [DllImport("scnet_png", CallingConvention = CallingConvention.Cdecl, EntryPoint = "png_image_free")]
    internal static extern void PngFree(PngImage* image);

    [DllImport("scnet_webp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPGetInfo")]
    internal static extern int WebPGetInfo(byte* input, nuint length, out int width, out int height);

    [DllImport("scnet_webp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPDecodeRGBAInto")]
    internal static extern byte* WebPDecode(byte* input, nuint length, byte* pixels, nuint size, int stride);

    [DllImport("scnet_webp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPEncodeLosslessRGBA")]
    internal static extern nuint WebPEncode(byte* pixels, int width, int height, int stride, out byte* output);

    [DllImport("scnet_webp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPFree")]
    internal static extern void WebPFree(byte* pointer);

    [DllImport("scnet_jpeg", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjInitDecompress")]
    internal static extern nint JpegDecoder();

    [DllImport("scnet_jpeg", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjInitCompress")]
    internal static extern nint JpegEncoder();

    [DllImport("scnet_jpeg", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjDestroy")]
    internal static extern int JpegDestroy(nint handle);

    [DllImport("scnet_jpeg", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjDecompressHeader3")]
    internal static extern int JpegHeaderUnix(nint handle, byte* input, nuint size, out int width, out int height,
        out int subsampling, out int colorSpace);

    [DllImport("scnet_jpeg", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjDecompress2")]
    internal static extern int JpegDecodeUnix(nint handle, byte* input, nuint size, byte* output, int width,
        int stride, int height, int pixelFormat, int flags);

    [DllImport("scnet_jpeg", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjCompress2")]
    internal static extern int JpegEncodeUnix(nint handle, byte* input, int width, int stride, int height,
        int pixelFormat, out byte* output, out nuint size, int subsampling, int quality, int flags);

    // TurboJPEG's classic API uses unsigned long: 32 bits on Windows, native-sized on Unix.
    [DllImport("scnet_jpeg", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjDecompressHeader3")]
    private static extern int JpegHeaderWindows(nint handle, byte* input, uint size, out int width,
        out int height, out int subsampling, out int colorSpace);

    [DllImport("scnet_jpeg", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjDecompress2")]
    private static extern int JpegDecodeWindows(nint handle, byte* input, uint size, byte* output,
        int width, int stride, int height, int pixelFormat, int flags);

    [DllImport("scnet_jpeg", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjCompress2")]
    private static extern int JpegEncodeWindows(nint handle, byte* input, int width, int stride,
        int height, int pixelFormat, out byte* output, out uint size, int subsampling, int quality, int flags);

    internal static int JpegHeader(nint handle, byte* input, nuint size, out int width, out int height,
        out int subsampling, out int colorSpace) => OperatingSystem.IsWindows()
        ? JpegHeaderWindows(handle, input, checked((uint)size), out width, out height, out subsampling, out colorSpace)
        : JpegHeaderUnix(handle, input, size, out width, out height, out subsampling, out colorSpace);

    internal static int JpegDecode(nint handle, byte* input, nuint size, byte* output, int width,
        int stride, int height, int pixelFormat, int flags) => OperatingSystem.IsWindows()
        ? JpegDecodeWindows(handle, input, checked((uint)size), output, width, stride, height, pixelFormat, flags)
        : JpegDecodeUnix(handle, input, size, output, width, stride, height, pixelFormat, flags);

    internal static int JpegEncode(nint handle, byte* input, int width, int stride, int height,
        int pixelFormat, out byte* output, out nuint size, int subsampling, int quality, int flags)
    {
        if (!OperatingSystem.IsWindows())
        {
            return JpegEncodeUnix(handle, input, width, stride, height, pixelFormat, out output,
                out size, subsampling, quality, flags);
        }
        var result = JpegEncodeWindows(handle, input, width, stride, height, pixelFormat, out output,
            out var windowsSize, subsampling, quality, flags);
        size = windowsSize;
        return result;
    }

    [DllImport("scnet_jpeg", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjFree")]
    internal static extern void JpegFree(byte* pointer);

    [DllImport("scnet_jpeg", CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjGetErrorStr2")]
    private static extern nint JpegError(nint handle);

    internal static InvalidDataException JpegFailure(nint handle) =>
        new(Marshal.PtrToStringUTF8(JpegError(handle)) ?? "JPEG operation failed.");
}
