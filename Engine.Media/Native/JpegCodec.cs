namespace Engine.Media.Native;

internal static unsafe class JpegCodec
{
    public static ImageInfo Identify(ReadOnlySpan<byte> data)
    {
        var handle = CodecNative.JpegDecoder();
        if (handle == 0)
        {
            throw CodecNative.JpegFailure(0);
        }
        try
        {
            fixed (byte* source = data)
            {
                if (CodecNative.JpegHeader(handle, source, (nuint)data.Length, out var width, out var height, out _, out _) != 0)
                {
                    throw CodecNative.JpegFailure(handle);
                }
                return new ImageInfo(width, height, ImageFormat.Jpeg);
            }
        }
        finally
        {
            CodecNative.JpegDestroy(handle);
        }
    }

    public static ImageBuffer Decode(ReadOnlySpan<byte> data, ImageInfo info)
    {
        var result = new ImageBuffer(info.Width, info.Height);
        var handle = CodecNative.JpegDecoder();
        if (handle == 0)
        {
            throw CodecNative.JpegFailure(0);
        }
        try
        {
            fixed (byte* source = data)
            fixed (byte* pixels = result.Bytes)
            {
                if (CodecNative.JpegDecode(handle, source, (nuint)data.Length, pixels, info.Width,
                        result.Stride, info.Height, 7, 0) != 0)
                {
                    result.Dispose();
                    throw CodecNative.JpegFailure(handle);
                }
            }
            return result;
        }
        finally
        {
            CodecNative.JpegDestroy(handle);
        }
    }

    public static byte[] Encode(ImageBuffer image, int quality)
    {
        var handle = CodecNative.JpegEncoder();
        if (handle == 0)
        {
            throw CodecNative.JpegFailure(0);
        }
        byte* output = null;
        try
        {
            fixed (byte* pixels = image.Bytes)
            {
                if (CodecNative.JpegEncode(handle, pixels, image.Width, image.Stride, image.Height,
                        7, out output, out var length, 2, quality, 0) != 0)
                {
                    throw CodecNative.JpegFailure(handle);
                }
                return new ReadOnlySpan<byte>(output, checked((int)length)).ToArray();
            }
        }
        finally
        {
            CodecNative.JpegFree(output);
            CodecNative.JpegDestroy(handle);
        }
    }
}
