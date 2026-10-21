using System.Runtime.InteropServices;

namespace Engine.Media.Native;

// libpng's simplified API png_image structure (PNG_IMAGE_VERSION = 1).
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct PngImage
{
    public nint Opaque;

    public uint Version;

    public uint Width;

    public uint Height;

    public uint Format;

    public uint Flags;

    public uint ColormapEntries;

    public uint WarningOrError;

    public fixed byte Message[64];
}
