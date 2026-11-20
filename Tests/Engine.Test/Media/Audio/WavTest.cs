using System.Buffers.Binary;

using Engine.Media.Audio;
using Engine.Media.Audio.Formats;

namespace Engine.Test.Media.Audio;

public class WavTest
{
    private sealed class NonSeekableReadStream(Stream stream) : Stream
    {
        public bool IsDisposed { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                stream.Dispose();
                IsDisposed = true;
            }

            base.Dispose(disposing);
        }
    }

    [Fact]
    public void NonSeekableInputIsBufferedAndOwnedAccordingToLeaveOpen()
    {
        var input = new NonSeekableReadStream(new MemoryStream(CreateWavBytes()));
        var source = new Wav.WavStreamingSource(input);

        source.Position = 1;
        var buffer = new byte[2];
        Assert.Equal(2, source.Read(buffer, 0, buffer.Length));
        Assert.Equal((short)200, BitConverter.ToInt16(buffer));
        Assert.False(input.IsDisposed);

        source.Dispose();

        Assert.True(input.IsDisposed);
    }

    [Fact]
    public void LeaveOpenPreservesNonSeekableInput()
    {
        using var input = new NonSeekableReadStream(new MemoryStream(CreateWavBytes()));
        using (var source = new Wav.WavStreamingSource(input, leaveOpen: true))
        {
            Assert.Equal(1, source.ChannelsCount);
        }

        Assert.False(input.IsDisposed);
    }

    [Fact]
    public void DuplicateStartsAtBeginningWithoutMovingOriginal()
    {
        using var input = new MemoryStream(CreateWavBytes());
        using var source = new Wav.WavStreamingSource(input, leaveOpen: true);
        var buffer = new byte[2];
        source.Read(buffer, 0, buffer.Length);
        var originalPosition = source.Position;

        using var duplicate = source.Duplicate();

        Assert.Equal(originalPosition, source.Position);
        Assert.Equal(0, duplicate.Position);
        Assert.Equal(2, duplicate.Read(buffer, 0, buffer.Length));
        Assert.Equal((short)100, BitConverter.ToInt16(buffer));
    }

    [Theory]
    [InlineData(1, 8000)]
    [InlineData(2, 44100)]
    public void PcmRoundTripUsesCanonicalLittleEndianHeaderAndLeavesStreamsOpen(int channels, int frequency)
    {
        var sound = new SoundData(channels, frequency, channels * 6);
        for (var i = 0; i < sound.Data.Length; i++)
        {
            sound.Data[i] = (short)(i * 1000 - 2000);
        }

        using var stream = new MemoryStream();
        Wav.Save(sound, stream);
        var bytes = stream.ToArray();
        Assert.Equal(bytes.Length - 8, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)));
        Assert.Equal(channels * 6, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(40)));
        Assert.Equal((short)-2000, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(44)));
        stream.Position = 0;
        var loaded = Wav.Load(stream);
        Assert.Equal(channels, loaded.ChannelsCount);
        Assert.Equal(frequency, loaded.SamplingFrequency);
        Assert.Equal(sound.Data, loaded.Data);
        Assert.True(stream.CanRead);
        Assert.True(stream.CanWrite);
    }

    [Fact]
    public void OddUnknownChunkAndExtendedFormatAreSkipped()
    {
        var original = CreateWavBytes();
        using var stream = new MemoryStream();
        stream.Write(original.AsSpan(0, 12));
        stream.Write("JUNK"u8);
        stream.Write([1, 0, 0, 0, 42, 0]);
        stream.Write(original.AsSpan(12, 24));
        stream.Write([0, 0]);
        stream.Write(original.AsSpan(36));
        var bytes = stream.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), bytes.Length - 8);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(26), 18);
        using var input = new MemoryStream(bytes);
        Assert.Equal(new short[] { 100, 200, 300 }, Wav.Load(input).Data);
    }

    [Fact]
    public void TruncatedDataIsRejectedAndDetectionPreservesPosition()
    {
        var bytes = CreateWavBytes();
        using var stream = new MemoryStream(bytes[..^1]);
        Assert.True(Wav.IsWavStream(stream));
        Assert.Equal(0, stream.Position);
        Assert.Throws<InvalidOperationException>(() => Wav.Load(stream));
        using var invalid = new MemoryStream(new byte[12]);
        Assert.False(Wav.IsWavStream(invalid));
        Assert.Equal(0, invalid.Position);
    }

    [Fact]
    public void DataChunkMayPrecedeFormatChunk()
    {
        var bytes = CreateWavBytes();
        using var input = new MemoryStream();
        input.Write(bytes.AsSpan(0, 12));
        input.Write(bytes.AsSpan(36));
        input.Write(bytes.AsSpan(12, 24));
        input.Position = 0;
        Assert.Equal(new short[] { 100, 200, 300 }, Wav.Load(input).Data);
    }

    private static byte[] CreateWavBytes()
    {
        var soundData = new SoundData(1, 8000, 6);
        soundData.Data[0] = 100;
        soundData.Data[1] = 200;
        soundData.Data[2] = 300;

        using var stream = new MemoryStream();
        Wav.Save(soundData, stream);
        return stream.ToArray();
    }
}
