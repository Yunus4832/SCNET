using System.Runtime.InteropServices;

using Engine.Core;
using Engine.Serialization;

namespace Engine.Media;

public static class Wav
{
    public static bool IsWavStream(Stream stream)
    {
        var binaryReader = new BinaryReader(stream);
        if (stream.Length - stream.Position < Utilities.SizeOf<WavHeader>())
        {
            return false;
        }

        var num = binaryReader.ReadInt32();
        var num2 = binaryReader.ReadInt32();
        var num3 = binaryReader.ReadInt32();
        stream.Position -= 12L;
        return num == MakeFourCc("RIFF") && num2 != 0 && num3 == MakeFourCc("WAVE");
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
        if (stream.Read(array, 0, array.Length) != array.Length)
        {
            throw new InvalidOperationException("Truncated WAV data.");
        }

        Buffer.BlockCopy(array, 0, soundData.Data, 0, array.Length);
        return soundData;
    }

    public static void Save(SoundData soundData, Stream stream)
    {
        if (soundData == null)
        {
            throw new ArgumentNullException(nameof(soundData));
        }

        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        var engineBinaryWriter = new EngineBinaryWriter(stream);
        var structure = default(WavHeader);
        structure.Riff = MakeFourCc("RIFF");
        structure.FileSize = Utilities.SizeOf<WavHeader>() + Utilities.SizeOf<FmtHeader>() +
                             Utilities.SizeOf<DataHeader>() + soundData.Data.Length;
        structure.Wave = MakeFourCc("WAVE");
        engineBinaryWriter.WriteStruct(structure);
        var structure2 = default(FmtHeader);
        structure2.Fmt = MakeFourCc("fmt ");
        structure2.FormatSize = 16;
        structure2.Type = 1;
        structure2.ChannelsCount = (short)soundData.ChannelsCount;
        structure2.SamplingFrequency = soundData.SamplingFrequency;
        structure2.BytesPerSecond = soundData.ChannelsCount * 2 * soundData.SamplingFrequency;
        structure2.BytesPerSample = (short)(soundData.ChannelsCount * 2);
        structure2.BitsPerChannel = 16;
        engineBinaryWriter.WriteStruct(structure2);
        var structure3 = default(DataHeader);
        structure3.Data = MakeFourCc("data");
        structure3.DataSize = soundData.Data.Length * 2;
        engineBinaryWriter.WriteStruct(structure3);
        var array = new byte[soundData.Data.Length * 2];
        Buffer.BlockCopy(soundData.Data, 0, array, 0, array.Length);
        stream.Write(array, 0, array.Length);
    }

    private static void ReadHeaders(Stream stream, out FmtHeader fmtHeader, out DataHeader dataHeader,
        out long dataStart)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (!BitConverter.IsLittleEndian)
        {
            throw new InvalidOperationException("Unsupported system endianness.");
        }

        if (!IsWavStream(stream))
        {
            throw new InvalidOperationException("Invalid WAV header.");
        }

        var engineBinaryReader = new EngineBinaryReader(stream);
        fmtHeader = default;
        dataHeader = default;
        dataStart = 0L;
        stream.Position += 12L;
        var flag = false;
        var flag2 = false;
        while (!flag || !flag2)
        {
            var num = engineBinaryReader.ReadInt32();
            if (num == MakeFourCc("fmt "))
            {
                stream.Position -= 4L;
                fmtHeader = engineBinaryReader.ReadStruct<FmtHeader>();
                flag = true;
            }
            else if (num == MakeFourCc("data"))
            {
                stream.Position -= 4L;
                dataHeader = engineBinaryReader.ReadStruct<DataHeader>();
                dataStart = stream.Position;
                flag2 = true;
            }
            else
            {
                var num2 = engineBinaryReader.ReadInt32();
                stream.Position += num2;
            }
        }

        if (fmtHeader.Type != 1 || fmtHeader.ChannelsCount < 1 || fmtHeader.ChannelsCount > 2 ||
            fmtHeader.SamplingFrequency < 8000 || fmtHeader.SamplingFrequency > 48000 ||
            fmtHeader.BitsPerChannel != 16)
        {
            throw new InvalidOperationException("Unsupported WAV format.");
        }
    }

    private static int MakeFourCc(string text)
    {
        return (int)(((uint)text[3] << 24) | ((uint)text[2] << 16) | ((uint)text[1] << 8) | text[0]);
    }

    public class WavStreamingSource : StreamingSource
    {
        private Stream _stream;

        private readonly bool _leaveOpen;

        private readonly int _channelsCount;

        private readonly int _samplingFrequency;

        private readonly long _bytesCount;

        private long _position;

        public override int ChannelsCount => _channelsCount;

        public override int SamplingFrequency => _samplingFrequency;

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

                _stream.Position = Utilities.SizeOf<WavHeader>() + num;
                _position = value;
            }
        }
#if ANDROID
        public WavStreamingSource(Stream stream, bool leaveOpen = false)
        {
            var memoryStream = new MemoryStream();
            stream.Position = 0L;
            stream.CopyTo(memoryStream);
            memoryStream.Position = 0L;
            _stream = memoryStream;
            _leaveOpen = leaveOpen;
            ReadHeaders(_stream, out var fmtHeader, out var dataHeader, out var dataStart);
            _channelsCount = fmtHeader.ChannelsCount;
            _samplingFrequency = fmtHeader.SamplingFrequency;
            _bytesCount = dataHeader.DataSize;
            _stream.Position = dataStart;
        }
#else
        public WavStreamingSource(Stream stream, bool leaveOpen = false)
        {
            _stream = stream;
            _leaveOpen = leaveOpen;
            ReadHeaders(stream, out var fmtHeader, out var dataHeader, out var dataStart);
            _channelsCount = fmtHeader.ChannelsCount;
            _samplingFrequency = fmtHeader.SamplingFrequency;
            _bytesCount = dataHeader.DataSize;
            stream.Position = dataStart;
        }
#endif
        public override void Dispose()
        {
            if (!_leaveOpen)
            {
                _stream.Dispose();
            }

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
#if ANDROID
        public override StreamingSource Duplicate()
        {
            var memoryStream = new MemoryStream();
            _stream.Position = 0L;
            _stream.CopyTo(memoryStream);
            memoryStream.Position = 0L;
            return new WavStreamingSource(memoryStream);
        }
#else
        public override StreamingSource Duplicate()
        {
            MemoryStream memoryStream = new MemoryStream();
            _stream.Position = 0L;
            _stream.CopyTo(memoryStream);
            return new WavStreamingSource(memoryStream);
        }

#endif
    }

    public struct WavInfo
    {
        public int ChannelsCount;

        public int SamplingFrequency;

        public int BytesCount;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct WavHeader
    {
        public int Riff;

        public int FileSize;

        public int Wave;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct FmtHeader
    {
        public int Fmt;

        public int FormatSize;

        public short Type;

        public short ChannelsCount;

        public int SamplingFrequency;

        public int BytesPerSecond;

        public short BytesPerSample;

        public short BitsPerChannel;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct DataHeader
    {
        public int Data;

        public int DataSize;
    }
}
