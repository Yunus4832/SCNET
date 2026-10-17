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
}
