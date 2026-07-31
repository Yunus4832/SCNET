using Engine.Media;

namespace Engine.Test.Media;

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
