using System.Text;

namespace Engine.Media.Native;

internal static unsafe class PngCodec
{
    public static ImageBuffer Decode(ReadOnlySpan<byte> data)
    {
        var image = new PngImage { Version = 1 };
        fixed (byte* source = data)
        {
            try
            {
                if (CodecNative.PngBeginRead(&image, source, (nuint)data.Length) == 0)
                {
                    throw Error(ref image);
                }
                var result = new ImageBuffer(checked((int)image.Width), checked((int)image.Height));
                image.Format = 3; // PNG_FORMAT_RGBA
                fixed (byte* pixels = result.Bytes)
                {
                    if (CodecNative.PngFinishRead(&image, null, pixels, result.Stride, null) == 0)
                    {
                        result.Dispose();
                        throw Error(ref image);
                    }
                }
                return result;
            }
            finally
            {
                CodecNative.PngFree(&image);
            }
        }
    }

    public static byte[] Encode(ImageBuffer buffer, bool alpha)
    {
        var image = new PngImage
        {
            Version = 1,
            Width = (uint)buffer.Width,
            Height = (uint)buffer.Height,
            Format = alpha ? 3u : 2u
        };
        var bytes = buffer.Bytes;
        byte[]? rgb = null;
        if (!alpha)
        {
            rgb = new byte[checked(buffer.Width * buffer.Height * 3)];
            for (int source = 0, destination = 0; source < bytes.Length; source += 4)
            {
                bytes.Slice(source, 3).CopyTo(rgb.AsSpan(destination, 3));
                destination += 3;
            }
            bytes = rgb;
        }
        fixed (byte* pixels = bytes)
        {
            try
            {
                nuint length = 0;
                if (CodecNative.PngWrite(&image, null, &length, 0, pixels, 0, null) == 0)
                {
                    throw Error(ref image);
                }
                var encoded = new byte[checked((int)length)];
                fixed (byte* output = encoded)
                {
                    if (CodecNative.PngWrite(&image, output, &length, 0, pixels, 0, null) == 0)
                    {
                        throw Error(ref image);
                    }
                }
                return encoded;
            }
            finally
            {
                CodecNative.PngFree(&image);
            }
        }
    }

    private static InvalidDataException Error(ref PngImage image)
    {
        fixed (byte* pointer = image.Message)
        {
            var message = new ReadOnlySpan<byte>(pointer, 64);
            var end = message.IndexOf((byte)0);
            return new InvalidDataException(Encoding.UTF8.GetString(end >= 0 ? message[..end] : message));
        }
    }
}
