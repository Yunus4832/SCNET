using System.Buffers.Binary;
using System.Text;

using Engine.Core.Mathematics;

namespace Engine.Media.Audio.Formats;

public static class Wav
{
    public static bool IsWavStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (stream.Length - stream.Position < 12)
        {
            return false;
        }

        var position = stream.Position;
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        try
        {
            return reader.ReadInt32() == MakeFourCc("RIFF") &&
                   reader.ReadUInt32() >= 4 &&
                   reader.ReadInt32() == MakeFourCc("WAVE");
        }
        finally
        {
            stream.Position = position;
        }
    }

    public static WavInfo GetInfo(Stream stream)
    {
        ReadHeaders(stream, out var fmtHeader, out var dataHeader, out _);
        var result = default(WavInfo);
        result.ChannelsCount = fmtHeader.ChannelsCount;
        result.SamplingFrequency = fmtHeader.SamplingFrequency;
        result.BytesCount = dataHeader.DataSize;
        return result;
    }

    public static StreamingSource Stream(Stream stream)
    {
        return new WavStreamingSource(stream);
    }

    public static SoundData Load(Stream stream)
    {
        ReadHeaders(stream, out var fmtHeader, out var dataHeader, out var dataStart);
        stream.Position = dataStart;
        var soundData = new SoundData(fmtHeader.ChannelsCount, fmtHeader.SamplingFrequency, dataHeader.DataSize);
        var array = new byte[dataHeader.DataSize];
        stream.ReadExactly(array);
        for (var i = 0; i < soundData.Data.Length; i++)
        {
            soundData.Data[i] = BinaryPrimitives.ReadInt16LittleEndian(array.AsSpan(i * 2));
        }

        return soundData;
    }

    public static void Save(SoundData soundData, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(soundData);
        ArgumentNullException.ThrowIfNull(stream);
        var dataSize = checked(soundData.Data.Length * 2);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(MakeFourCc("RIFF"));
        writer.Write(checked(36 + dataSize));
        writer.Write(MakeFourCc("WAVE"));
        writer.Write(MakeFourCc("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)soundData.ChannelsCount);
        writer.Write(soundData.SamplingFrequency);
        writer.Write(checked(soundData.ChannelsCount * 2 * soundData.SamplingFrequency));
        writer.Write((short)(soundData.ChannelsCount * 2));
        writer.Write((short)16);
        writer.Write(MakeFourCc("data"));
        writer.Write(dataSize);
        var bytes = new byte[dataSize];
        for (var i = 0; i < soundData.Data.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2), soundData.Data[i]);
        }

        writer.Write(bytes);
    }

    private static void ReadHeaders(
        Stream stream,
        out FmtHeader fmtHeader,
        out DataHeader dataHeader,
        out long dataStart
    )
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!IsWavStream(stream))
        {
            throw new InvalidOperationException("Invalid WAV header.");
        }

        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var start = stream.Position;
        reader.ReadInt32();
        var end = start + 8 + reader.ReadUInt32();
        reader.ReadInt32();
        if (end > stream.Length)
        {
            throw new InvalidOperationException("Truncated WAV data.");
        }

        fmtHeader = default;
        dataHeader = default;
        dataStart = 0;
        var hasFormat = false;
        var hasData = false;
        while (!hasFormat || !hasData)
        {
            if (end - stream.Position < 8)
            {
                throw new InvalidOperationException("Missing WAV format or data chunk.");
            }

            var id = reader.ReadInt32();
            var size = reader.ReadUInt32();
            var chunkStart = stream.Position;
            var chunkEnd = chunkStart + size;
            var paddedEnd = chunkEnd + (size & 1);
            if (paddedEnd > end || size > int.MaxValue)
            {
                throw new InvalidOperationException("Truncated or oversized WAV chunk.");
            }

            if (id == MakeFourCc("fmt ") && !hasFormat)
            {
                if (size < 16)
                {
                    throw new InvalidOperationException("Invalid WAV format chunk.");
                }

                fmtHeader.Type = reader.ReadInt16();
                fmtHeader.ChannelsCount = reader.ReadInt16();
                fmtHeader.SamplingFrequency = reader.ReadInt32();
                fmtHeader.BytesPerSecond = reader.ReadInt32();
                fmtHeader.BytesPerSample = reader.ReadInt16();
                fmtHeader.BitsPerChannel = reader.ReadInt16();
                hasFormat = true;
            }
            else if (id == MakeFourCc("data") && !hasData)
            {
                dataHeader.DataSize = (int)size;
                dataStart = chunkStart;
                hasData = true;
            }

            stream.Position = paddedEnd;
        }

        if (fmtHeader.Type != 1 ||
            fmtHeader.ChannelsCount is < 1 or > 2 ||
            fmtHeader.SamplingFrequency is < 8000 or > 48000 ||
            fmtHeader.BitsPerChannel != 16 ||
            fmtHeader.BytesPerSample != fmtHeader.ChannelsCount * 2 ||
            fmtHeader.BytesPerSecond != fmtHeader.SamplingFrequency * fmtHeader.BytesPerSample ||
            dataHeader.DataSize % fmtHeader.BytesPerSample != 0)
        {
            throw new InvalidOperationException("Unsupported WAV format.");
        }

        stream.Position = dataStart;
    }

    private static int MakeFourCc(string text)
    {
        return (int)(((uint)text[3] << 24) | ((uint)text[2] << 16) | ((uint)text[1] << 8) | text[0]);
    }

    public class WavStreamingSource : StreamingSource
    {
        private Stream _stream;

        private readonly bool _disposeStream;

        private readonly Stream? _sourceStreamToDispose;

        private readonly long _bytesCount;

        private readonly long _streamStart;

        private readonly long _dataStart;

        private long _position;

        public override int ChannelsCount { get; }

        public override int SamplingFrequency { get; }

        public override long BytesCount => _bytesCount;

        public override long Position
        {
            get => _position;
            set
            {
                if (!_stream.CanSeek)
                {
                    throw new NotSupportedException("Underlying stream cannot be seeked.");
                }

                var num = value * ChannelsCount * 2;
                if (num < 0 || num > BytesCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(num));
                }

                _stream.Position = _dataStart + num;
                _position = value;
            }
        }

        public WavStreamingSource(Stream stream, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (!stream.CanRead)
            {
                throw new ArgumentException("Stream must be readable.", nameof(stream));
            }

            if (stream.CanSeek)
            {
                _stream = stream;
                _streamStart = stream.Position;
                _disposeStream = !leaveOpen;
            }
            else
            {
                var memoryStream = new MemoryStream();
                try
                {
                    stream.CopyTo(memoryStream);
                    memoryStream.Position = 0L;
                }
                catch
                {
                    memoryStream.Dispose();
                    throw;
                }

                _stream = memoryStream;
                _streamStart = 0L;
                _disposeStream = true;
                _sourceStreamToDispose = leaveOpen ? null : stream;
            }

            ReadHeaders(_stream, out var fmtHeader, out var dataHeader, out var dataStart);
            ChannelsCount = fmtHeader.ChannelsCount;
            SamplingFrequency = fmtHeader.SamplingFrequency;
            _bytesCount = dataHeader.DataSize;
            _dataStart = dataStart;
            _stream.Position = _dataStart;
        }

        public override void Dispose()
        {
            if (_disposeStream)
            {
                _stream.Dispose();
            }

            _sourceStreamToDispose?.Dispose();
            _stream = null!;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count % (2 * ChannelsCount) != 0)
            {
                throw new InvalidOperationException("Cannot read partial samples.");
            }

            count = (int)MathUtils.Min(count, _bytesCount - _position * 2 * ChannelsCount);
            var num = _stream.Read(buffer, offset, count);
            _position += num / 2 / ChannelsCount;
            return num;
        }

        public override StreamingSource Duplicate()
        {
            var originalPosition = _stream.Position;
            var memoryStream = new MemoryStream();
            try
            {
                _stream.Position = _streamStart;
                _stream.CopyTo(memoryStream);
                memoryStream.Position = 0L;
            }
            catch
            {
                memoryStream.Dispose();
                throw;
            }
            finally
            {
                _stream.Position = originalPosition;
            }

            return new WavStreamingSource(memoryStream);
        }
    }

    public struct WavInfo
    {
        public int ChannelsCount;

        public int SamplingFrequency;

        public int BytesCount;
    }

    private struct FmtHeader
    {
        public short Type;

        public short ChannelsCount;

        public int SamplingFrequency;

        public int BytesPerSecond;

        public short BytesPerSample;

        public short BitsPerChannel;
    }

    private struct DataHeader
    {
        public int DataSize;
    }
}
