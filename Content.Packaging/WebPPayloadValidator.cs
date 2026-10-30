using System.Buffers.Binary;

namespace Content.Packaging;

internal static class WebPPayloadValidator
{
    public static void Validate(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12 ||
            !data[..4].SequenceEqual("RIFF"u8) ||
            !data.Slice(8, 4).SequenceEqual("WEBP"u8) ||
            BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4)) != data.Length - 8)
        {
            throw new ContentPackageException("WebP RIFF length or signature is invalid.");
        }

        var offset = 12;
        var images = 0;
        while (offset < data.Length)
        {
            if (data.Length - offset < 8)
            {
                throw new ContentPackageException("WebP chunk header is truncated.");
            }

            var kind = data.Slice(offset, 4);
            var length = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 4, 4));
            var end = offset + 8L + length + (length & 1);
            if (end > data.Length)
            {
                throw new ContentPackageException("WebP chunk is truncated.");
            }

            if (kind.SequenceEqual("ANIM"u8) ||
                kind.SequenceEqual("ANMF"u8) ||
                kind.SequenceEqual("VP8X"u8) && (length != 10 || (data[offset + 8] & 2) != 0))
            {
                throw new ContentPackageException("WebP payload must be a valid static image.");
            }

            if (kind.SequenceEqual("VP8 "u8) || kind.SequenceEqual("VP8L"u8))
            {
                images++;
            }

            offset = (int)end;
        }

        if (images != 1)
        {
            throw new ContentPackageException("WebP payload must contain exactly one image bitstream.");
        }
    }
}
