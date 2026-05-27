using NAudio.Wave;

using NLayer.NAudioSupport;

namespace Engine.Media;

public static class Mp3
{
    public class Mp3StreamingSource : StreamingSource
    {
        public readonly Mp3FileReaderBase Reader;

        public readonly Stream SoundStream;

        private long _streamPosition;

        public override int ChannelsCount => Reader.WaveFormat.Channels;

        public override int SamplingFrequency => Reader.WaveFormat.SampleRate;

        public override long Position
        {
            get => _streamPosition;
            set
            {
                if (!Reader.CanSeek)
                {
                    throw new NotSupportedException("Underlying stream cannot be seeked.");
                }

                var num = value * ChannelsCount * 4;
                if (num < 0 || num > BytesCount)
                {
                    throw new InvalidOperationException("Invalid range");
                }

                Reader.Position = num;
                _streamPosition = value;
            }
        }

        public override long BytesCount => Reader.Length / 2;

        public Mp3StreamingSource(Stream stream)
        {
            SoundStream = new MemoryStream();
            stream.Position = 0L;
            stream.CopyTo(SoundStream);
            SoundStream.Position = 0L;
            Reader = new Mp3FileReaderBase(SoundStream, wf => new Mp3FrameDecompressor(wf));
        }

        public override void Dispose()
        {
            Reader.Dispose();
            SoundStream.Dispose();
            GC.SuppressFinalize(this);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset < 0 ||
                count < 0 ||
                offset + count > buffer.Length
               )
            {
                throw new InvalidOperationException("Invalid range.");
            }

            count = (int)Math.Min(count, BytesCount - Position);
            var sample = new byte[count * 2];
            var num = Reader.Read(sample, 0, count * 2);
            for (var i = 0; i < num; i += 4)
            {
                var sample32Bit = BitConverter.ToSingle(sample, i);
                var sample16Bit = (short)(sample32Bit * short.MaxValue);
                var sampleBytes = BitConverter.GetBytes(sample16Bit);
                buffer[offset++] = sampleBytes[0];
                buffer[offset++] = sampleBytes[1];
            }

            _streamPosition += num / 2 / ChannelsCount;
            return num / 2;
        }

        /// <summary>
        /// 复制出一个新的流
        /// </summary>
        /// <returns></returns>
        public override StreamingSource Duplicate()
        {
            MemoryStream memoryStream = new();
            SoundStream.Position = 0L;
            SoundStream.CopyTo(memoryStream);
            memoryStream.Position = 0L;
            return new Mp3StreamingSource(memoryStream);
        }
    }

    public static bool IsMp3Stream(Stream stream)
    {
        var position = stream.Position;
        stream.Position = 0;
        var result = Id3v2Tag.ReadTag(stream) != null;
        stream.Position = position;
        return result;
    }

    public static StreamingSource Stream(Stream stream)
    {
        return new Mp3StreamingSource(stream);
    }

    public static SoundData Load(Stream stream)
    {
        using var streamingSource = Stream(stream);
        if (streamingSource.BytesCount > int.MaxValue)
        {
            throw new InvalidOperationException("Sound data too long.");
        }

        var array = new byte[(int)streamingSource.BytesCount];
        streamingSource.Read(array, 0, array.Length);
        SoundData soundData = new(streamingSource.ChannelsCount, streamingSource.SamplingFrequency, array.Length);
        Buffer.BlockCopy(array, 0, soundData.Data, 0, array.Length);
        return soundData;
    }
}
