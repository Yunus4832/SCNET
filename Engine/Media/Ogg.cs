using Engine.Core;

using NVorbis;

namespace Engine.Media;

public static class Ogg
{
    public static bool IsOggStream(Stream stream)
    {
        var position = stream.Position;
        var num = stream.ReadByte();
        var num2 = stream.ReadByte();
        var num3 = stream.ReadByte();
        var num4 = stream.ReadByte();
        stream.Position = position;
        if (num == 79 && num2 == 103 && num3 == 103)
        {
            return num4 == 83;
        }

        return false;
    }

    public static StreamingSource Stream(Stream stream, bool leaveOpen = false)
    {
        return new OggStreamingSource(stream, leaveOpen);
    }

    public static SoundData Load(Stream stream)
    {
        using var streamingSource = Stream(stream, true);
        if (streamingSource.BytesCount > int.MaxValue)
        {
            throw new InvalidOperationException("Sound data too long.");
        }

        var array = new byte[(int)streamingSource.BytesCount];
        streamingSource.Read(array, 0, array.Length);
        var soundData = new SoundData(streamingSource.ChannelsCount, streamingSource.SamplingFrequency,
            array.Length);
        Buffer.BlockCopy(array, 0, soundData.Data, 0, array.Length);
        return soundData;
    }

    public class OggStreamingSource : StreamingSource
    {
        private readonly VorbisReader _reader;

        private readonly float[] _samples = new float[1024];

        private readonly Stream _stream;

        public OggStreamingSource(Stream stream, bool leaveOpen = false)
        {
            var memoryStream = new MemoryStream();
            stream.Position = 0L;
            stream.CopyTo(memoryStream);
            memoryStream.Position = 0L;
            _stream = memoryStream;
            _reader = new VorbisReader(_stream, leaveOpen);
        }

        public override int ChannelsCount => _reader.Channels;

        public override int SamplingFrequency => _reader.SampleRate;

        public override long Position
        {
            get => _reader.SamplePosition;
            set
            {
                try
                {
                    _reader.SamplePosition = value;
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Ignore seek errors, especially when stream is at end
                }
            }
        }

        public override long BytesCount => _reader.TotalSamples * 2;

        public override void Dispose()
        {
            _reader.Dispose();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new InvalidOperationException("Invalid range.");
            }

            var num = 0;
            while (count >= 2)
            {
                var count2 = MathUtils.Min(count / 2, _samples.Length);
                var num2 = _reader.ReadSamples(_samples, 0, count2);
                if (num2 == 0)
                {
                    break;
                }

                num += num2;
                if (BitConverter.IsLittleEndian)
                {
                    for (var i = 0; i < num2; i++)
                    {
                        var num3 = (short)(_samples[i] * 32767f);
                        buffer[offset++] = (byte)num3;
                        buffer[offset++] = (byte)(num3 >> 8);
                    }
                }
                else
                {
                    for (var j = 0; j < num2; j++)
                    {
                        var num4 = (short)(_samples[j] * 32767f);
                        buffer[offset++] = (byte)(num4 >> 8);
                        buffer[offset++] = (byte)num4;
                    }
                }

                count -= num2 * 2;
            }

            return num * 2;
        }

        /// <summary>
        ///     复制出一个新的流
        /// </summary>
        /// <returns></returns>
        public override StreamingSource Duplicate()
        {
            var memoryStream = new MemoryStream();
            _stream.Position = 0L;
            _stream.CopyTo(memoryStream);
            memoryStream.Position = 0L;
            return new OggStreamingSource(memoryStream);
        }
    }
}
