using System.IO.Compression;

namespace Game.Protocol.Serialization;

public static class ProtocolFrameCodec
{
    private enum WireCompression : byte
    {
        None = 0,

        Deflate = 1
    }

    private const int _compressionThreshold = 256;

    public const int MaximumPayloadLength = 64 * 1024 * 1024;

    public static byte[] EncodeFrame(Stream stream, CompressionPolicy compressionPolicy)
    {
        stream.Position = 0L;
        var data = new byte[stream.Length];
        stream.ReadExactly(data, 0, (int)stream.Length);
        return EncodeFrame(data, compressionPolicy);
    }

    private static byte[] EncodeFrame(byte[] data, CompressionPolicy compressionPolicy)
    {
        var payload = data;
        var compression = WireCompression.None;
        if (compressionPolicy != CompressionPolicy.None &&
            (compressionPolicy == CompressionPolicy.Always || data.Length >= _compressionThreshold))
        {
            var compressed = Deflate(data);
            if (compressionPolicy == CompressionPolicy.Always || compressed.Length + 5 < data.Length * 9 / 10)
            {
                payload = compressed;
                compression = WireCompression.Deflate;
            }
        }

        using var frame = new MemoryStream(payload.Length + 5);
        using var writer = new BinaryWriter(frame);
        writer.Write((byte)compression);
        writer.Write(data.Length);
        writer.Write(payload);
        return frame.ToArray();
    }

    private static byte[] Deflate(byte[] data)
    {
        using var outStream = new MemoryStream();
        using (var zipStream = new DeflateStream(outStream, CompressionLevel.Fastest, true))
        {
            zipStream.Write(data, 0, data.Length);
        }

        var streamArray = outStream.ToArray();

        return streamArray;
    }

    public static byte[] DecodeFrame(byte[] frame)
    {
        using var frameStream = new MemoryStream(frame, false);
        using var reader = new BinaryReader(frameStream);
        if (frame.Length < 5)
        {
            throw new InvalidDataException("Network frame is truncated.");
        }

        var compression = (WireCompression)reader.ReadByte();
        var rawLength = reader.ReadInt32();
        if (rawLength is < 0 or > MaximumPayloadLength)
        {
            throw new InvalidDataException($"Invalid network frame size: {rawLength}.");
        }

        var inputBytes = reader.ReadBytes((int)(frameStream.Length - frameStream.Position));
        if (compression == WireCompression.None)
        {
            return inputBytes.Length != rawLength
                ? throw new InvalidDataException("Uncompressed network frame length does not match its header.")
                : inputBytes;
        }

        if (compression != WireCompression.Deflate)
        {
            throw new InvalidDataException($"Unsupported network compression codec: {(byte)compression}.");
        }

        var output = new byte[rawLength];
        using var inputStream = new MemoryStream(inputBytes);
        using var zipStream = new DeflateStream(inputStream, CompressionMode.Decompress);
        zipStream.ReadExactly(output);
        return zipStream.ReadByte() != -1
            ? throw new InvalidDataException("Network frame expands beyond its declared size.")
            : output;
    }
}
