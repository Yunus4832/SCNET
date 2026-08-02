using System.IO.Compression;

namespace Game.Network.Serialization;

public sealed record EncodedTerrainChunk(Point2 Coords, byte[] Payload);

public static class NetworkChunkCodec
{
    private enum CellEncoding : byte
    {
        Raw,
        Rle
    }

    private enum PayloadCompression : byte
    {
        None,
        Deflate
    }

    private const byte _version = 1;
    private const int _cellCount = 16 * 16 * 256;
    private const int _shaftCount = 16 * 16;
    private const int _rawBodySize = _shaftCount + _cellCount * sizeof(int);
    private const int _maxPayloadSize = 2 * 1024 * 1024;

    public static EncodedTerrainChunk Encode(TerrainChunk chunk)
    {
        return Encode(chunk.Coords, chunk.Cells, chunk.Shafts);
    }

    internal static EncodedTerrainChunk Encode(NetworkChunkSnapshot snapshot)
    {
        return Encode(snapshot.Coords, snapshot.Cells, snapshot.Shafts);
    }

    private static EncodedTerrainChunk Encode(Point2 coords, int[] cells, long[] shafts)
    {
        using var rleStream = new MemoryStream(_rawBodySize / 4);
        using (var writer = new BinaryWriter(rleStream, System.Text.Encoding.UTF8, true))
        {
            WriteClimate(writer, shafts);
            WriteRleCells(writer, cells);
        }

        var rle = rleStream.ToArray();
        var encoding = CellEncoding.Rle;
        byte[] body;
        if (rle.Length < _rawBodySize)
        {
            body = rle;
        }
        else
        {
            encoding = CellEncoding.Raw;
            using var rawStream = new MemoryStream(_rawBodySize);
            using var writer = new BinaryWriter(rawStream);
            WriteClimate(writer, shafts);
            for (var y = 0; y < 256; y++)
            for (var z = 0; z < 16; z++)
            for (var x = 0; x < 16; x++)
            {
                writer.Write(Terrain.ReplaceLight(cells[TerrainChunk.CalculateCellIndex(x, y, z)], 0));
            }

            body = rawStream.ToArray();
        }

        var compressed = Deflate(body);
        var compression = compressed.Length + 7 < body.Length * 9 / 10
            ? PayloadCompression.Deflate
            : PayloadCompression.None;
        var payloadBody = compression == PayloadCompression.Deflate ? compressed : body;

        using var payloadStream = new MemoryStream(payloadBody.Length + 7);
        using var payloadWriter = new BinaryWriter(payloadStream);
        payloadWriter.Write(_version);
        payloadWriter.Write((byte)encoding);
        payloadWriter.Write((byte)compression);
        payloadWriter.Write(body.Length);
        payloadWriter.Write(payloadBody);
        return new EncodedTerrainChunk(coords, payloadStream.ToArray());
    }

    public static TerrainChunk Decode(Point2 coords, byte[] payload)
    {
        if (payload.Length is < 7 or > _maxPayloadSize)
        {
            throw new InvalidDataException($"Invalid encoded terrain chunk size: {payload.Length}.");
        }

        using var payloadStream = new MemoryStream(payload, false);
        using var reader = new BinaryReader(payloadStream);
        var version = reader.ReadByte();
        if (version != _version)
        {
            throw new InvalidDataException($"Unsupported terrain chunk codec version: {version}.");
        }

        var encoding = (CellEncoding)reader.ReadByte();
        var compression = (PayloadCompression)reader.ReadByte();
        var rawLength = reader.ReadInt32();
        if (rawLength is < _shaftCount or > _maxPayloadSize)
        {
            throw new InvalidDataException($"Invalid terrain chunk body size: {rawLength}.");
        }

        var encodedBody = reader.ReadBytes((int)(payloadStream.Length - payloadStream.Position));
        var body = compression switch
        {
            PayloadCompression.None => encodedBody,
            PayloadCompression.Deflate => Inflate(encodedBody, rawLength),
            _ => throw new InvalidDataException($"Unsupported terrain payload compression: {(byte)compression}.")
        };
        if (body.Length != rawLength)
        {
            throw new InvalidDataException("Terrain chunk body length does not match its header.");
        }

        var chunk = new TerrainChunk(null!, coords.X, coords.Y);
        using var bodyReader = new BinaryReader(new MemoryStream(body, false));
        ReadClimate(bodyReader, chunk);
        switch (encoding)
        {
            case CellEncoding.Raw:
                ReadRawCells(bodyReader, chunk);
                break;
            case CellEncoding.Rle:
                ReadRleCells(bodyReader, chunk);
                break;
            default:
                throw new InvalidDataException($"Unsupported terrain cell encoding: {(byte)encoding}.");
        }

        return bodyReader.BaseStream.Position != bodyReader.BaseStream.Length
            ? throw new InvalidDataException("Terrain chunk body contains trailing data.")
            : chunk;
    }

    private static void WriteClimate(BinaryWriter writer, long[] shafts)
    {
        for (var z = 0; z < 16; z++)
        for (var x = 0; x < 16; x++)
        {
            var shaft = shafts[x + z * 16];
            writer.Write((byte)((Terrain.ExtractTemperature(shaft) << 4) | Terrain.ExtractHumidity(shaft)));
        }
    }

    private static void ReadClimate(BinaryReader reader, TerrainChunk chunk)
    {
        for (var z = 0; z < 16; z++)
        for (var x = 0; x < 16; x++)
        {
            var climate = reader.ReadByte();
            var shaft = Terrain.ReplaceTemperature(0, climate >> 4);
            shaft = Terrain.ReplaceHumidity(shaft, climate & 0xF);
            chunk.SetShaftValueFast(x, z, shaft);
        }
    }

    private static void WriteRleCells(BinaryWriter writer, int[] cells)
    {
        var value = 0;
        var count = 0;
        for (var y = 0; y < 256; y++)
        for (var z = 0; z < 16; z++)
        for (var x = 0; x < 16; x++)
        {
            var next = Terrain.ReplaceLight(cells[TerrainChunk.CalculateCellIndex(x, y, z)], 0);
            if (count == 0)
            {
                value = next;
                count = 1;
            }
            else if (next == value && count < ushort.MaxValue)
            {
                count++;
            }
            else
            {
                writer.Write(value);
                writer.Write((ushort)count);
                value = next;
                count = 1;
            }
        }

        writer.Write(value);
        writer.Write((ushort)count);
    }

    private static void ReadRleCells(BinaryReader reader, TerrainChunk chunk)
    {
        var index = 0;
        while (index < _cellCount)
        {
            if (reader.BaseStream.Length - reader.BaseStream.Position < 6)
            {
                throw new InvalidDataException("Terrain RLE stream is truncated.");
            }

            var value = Terrain.ReplaceLight(reader.ReadInt32(), 0);
            var count = reader.ReadUInt16();
            if (count == 0 || index + count > _cellCount)
            {
                throw new InvalidDataException("Terrain RLE run exceeds the chunk cell count.");
            }

            for (var i = 0; i < count; i++, index++)
            {
                var y = index / 256;
                var layerIndex = index % 256;
                var z = layerIndex / 16;
                var x = layerIndex % 16;
                chunk.SetCellValueFast(x, y, z, value);
            }
        }
    }

    private static void ReadRawCells(BinaryReader reader, TerrainChunk chunk)
    {
        for (var y = 0; y < 256; y++)
        for (var z = 0; z < 16; z++)
        for (var x = 0; x < 16; x++)
        {
            chunk.SetCellValueFast(x, y, z, Terrain.ReplaceLight(reader.ReadInt32(), 0));
        }
    }

    private static byte[] Deflate(byte[] input)
    {
        using var output = new MemoryStream();
        using (var stream = new DeflateStream(output, CompressionLevel.Fastest, true))
        {
            stream.Write(input);
        }

        return output.ToArray();
    }

    private static byte[] Inflate(byte[] input, int expectedLength)
    {
        var output = new byte[expectedLength];
        using var stream = new DeflateStream(new MemoryStream(input, false), CompressionMode.Decompress);
        stream.ReadExactly(output);
        return stream.ReadByte() != -1
            ? throw new InvalidDataException("Terrain chunk decompressed beyond its declared size.")
            : output;
    }
}
