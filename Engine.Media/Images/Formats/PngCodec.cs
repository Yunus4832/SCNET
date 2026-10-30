using System.Buffers.Binary;
using System.Text;

using Engine.Media.Images.Native;

namespace Engine.Media.Images.Formats;

internal static unsafe class PngCodec
{
    public static ImageInfo Identify(ReadOnlySpan<byte> data)
    {
        if (data.Length < 33 ||
            BinaryPrimitives.ReadUInt32BigEndian(data.Slice(8, 4)) != 13 ||
            !data.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            throw new InvalidDataException("Invalid PNG header.");
        }

        var width = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(20, 4));
        if (width > int.MaxValue || height > int.MaxValue)
        {
            throw new InvalidDataException("Invalid PNG dimensions.");
        }

        var frames = 1;
        var offset = 8;
        var sawEnd = false;
        while (offset <= data.Length - 12)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
            if (length > int.MaxValue || offset + length + 12 > data.Length)
            {
                throw new InvalidDataException("Truncated PNG chunk.");
            }

            var type = data.Slice(offset + 4, 4);
            if (type.SequenceEqual("acTL"u8))
            {
                if (length != 8)
                {
                    throw new InvalidDataException("Invalid PNG animation header.");
                }

                frames = Math.Max(
                    2,
                    (int)Math.Min(int.MaxValue, BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset + 8, 4)))
                );
            }

            if (type.SequenceEqual("IEND"u8))
            {
                sawEnd = true;
                break;
            }

            offset = checked(offset + (int)length + 12);
        }

        if (!sawEnd)
        {
            throw new InvalidDataException("PNG end chunk is missing.");
        }

        return new ImageInfo((int)width, (int)height, ImageFormat.Png, frames);
    }

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
