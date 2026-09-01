using NAudio.Flac;

namespace Engine.Media;

public static class Flac
{
    public class FlacStreamingSource : StreamingSource
    {
        public readonly FlacReader Reader;

        public readonly Stream SoundStream;

        private long _soundPosition;

        public override int ChannelsCount => Reader.WaveFormat.Channels;

        public override int SamplingFrequency => Reader.WaveFormat.SampleRate;

        public override long Position
        {
            get => _soundPosition;
            set
            {
                Reader.Position = value;
                if (!Reader.CanSeek)
                {
                    throw new NotSupportedException("Underlying stream cannot be seeked.");
                }

                var num = value * ChannelsCount * 2;
                if (num < 0 || num > BytesCount)
                {
                    throw new InvalidOperationException("Invalid range.");
                }

                Reader.Position = num;
                _soundPosition = value;
            }
        }

        public override long BytesCount => Reader.Length;

        public FlacStreamingSource(Stream stream)
        {
            SoundStream = new MemoryStream();
            stream.Position = 0L;
            stream.CopyTo(SoundStream);
            SoundStream.Position = 0L;
            Reader = new FlacReader(SoundStream);
        }

        public override void Dispose()
        {
            Reader.Dispose();
            SoundStream.Dispose();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new InvalidOperationException("Invalid range.");
            }

            var num = Reader.Read(buffer, offset, (int)Math.Min(count, BytesCount - Position));
            _soundPosition += num / 2 / ChannelsCount;
            return num;
        }

        /// <summary>
        ///     复制出一个新的流
        /// </summary>
        public override StreamingSource Duplicate()
        {
            MemoryStream memoryStream = new();
            SoundStream.Position = 0L;
            SoundStream.CopyTo(memoryStream);
            memoryStream.Position = 0L;
            return new FlacStreamingSource(memoryStream);
        }
    }

    public static bool IsFlacStream(Stream stream)
    {
        var position = stream.Position;
        stream.Position = 0;
        ID3v2.SkipTag(stream);
        var beginSync = new byte[4];
        var read = stream.Read(beginSync, 0, beginSync.Length);
        stream.Position = position;
        return read < beginSync.Length
            ? throw new EndOfStreamException("Can not read \"fLaC\" sync.")
            : beginSync[0] == 0x66 && beginSync[1] == 0x4C && beginSync[2] == 0x61 && beginSync[3] == 0x43;
    }

    public static StreamingSource Stream(Stream stream)
    {
        return new FlacStreamingSource(stream);
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
        var soundData = new SoundData(streamingSource.ChannelsCount, streamingSource.SamplingFrequency, array.Length);
        Buffer.BlockCopy(array, 0, soundData.Data, 0, array.Length);
        return soundData;
    }
}
