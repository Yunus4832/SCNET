namespace Engine.Media.Native;

internal static unsafe class WebPCodec
{
    public static ImageInfo Identify(ReadOnlySpan<byte> data)
    {
        fixed (byte* source = data)
        {
            if (CodecNative.WebPGetInfo(source, (nuint)data.Length, out var width, out var height) == 0)
            {
                throw new InvalidDataException("Invalid WebP header.");
            }
            // VP8X animation flag. The still-image codec deliberately rejects animated containers.
            var frames = data.Length >= 21 && data.Slice(12, 4).SequenceEqual("VP8X"u8) && (data[20] & 2) != 0 ? 2 : 1;
            return new ImageInfo(width, height, ImageFormat.WebP, frames);
        }
    }

    public static ImageBuffer Decode(ReadOnlySpan<byte> data, ImageInfo info)
    {
        var result = new ImageBuffer(info.Width, info.Height);
        fixed (byte* source = data)
        fixed (byte* pixels = result.Bytes)
        {
            if (CodecNative.WebPDecode(source, (nuint)data.Length, pixels, (nuint)result.Bytes.Length, result.Stride) is null)
            {
                result.Dispose();
                throw new InvalidDataException("Invalid WebP pixel data.");
            }
        }
        return result;
    }

    public static byte[] Encode(ImageBuffer image)
    {
        fixed (byte* pixels = image.Bytes)
        {
            var length = CodecNative.WebPEncode(pixels, image.Width, image.Height, image.Stride, out var output);
            try
            {
                if (length == 0 || output is null)
                {
                    throw new InvalidDataException("WebP encoding failed.");
                }
                return new ReadOnlySpan<byte>(output, checked((int)length)).ToArray();
            }
            finally
            {
                CodecNative.WebPFree(output);
            }
        }
    }
}
