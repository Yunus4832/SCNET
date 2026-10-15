using Engine.Core;

namespace Engine.Media;

internal class PeekStream : Stream
{
    private readonly byte[] _buffer;

    private readonly int _end;

    private readonly Stream _stream;

    private long _position;

    public PeekStream(Stream stream, int peekSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(peekSize);
        if (!stream.CanRead)
        {
            throw new ArgumentException("Stream is not readable.");
        }

        _stream = stream;
        _buffer = new byte[peekSize];
        _end = stream.Read(_buffer, 0, peekSize);
    }

    public override bool CanRead => true;

    public override bool CanWrite => false;

    public override bool CanSeek => _stream.CanSeek;

    public override long Length => _stream.Length;

    public override long Position
    {
        get => CanSeek ? _position : throw new NotSupportedException();
        set
        {
            if (!CanSeek)
            {
                throw new NotSupportedException();
            }

            _position = value;
            _stream.Position = MathUtils.Max(_position, _end);
        }
    }

    public MemoryStream GetInitialBytesStream()
    {
        return new MemoryStream(_buffer, 0, _end, false);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin:
                Position = offset;
                break;
            case SeekOrigin.End:
                Position = Length + offset;
                break;
            case SeekOrigin.Current:
                Position += offset;
                break;
            default:
                throw new ArgumentException("Invalid origin.", nameof(origin));
        }

        return Position;
    }

    public override void SetLength(long value)
    {
        _stream.SetLength(value);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        var num = 0;
        if (_position < _end)
        {
            var num2 = MathUtils.Min(_end - (int)_position, count);
            Array.Copy(_buffer, (int)_position, buffer, offset, num2);
            _position += num2;
            offset += num2;
            count -= num2;
            num += num2;
        }

        if (count <= 0)
        {
            return num;
        }

        num += _stream.Read(buffer, offset, count);
        _position += num;

        return num;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public override int ReadByte()
    {
        if (_position < _end)
        {
            return _buffer[_position++];
        }

        var num = _stream.ReadByte();
        if (num >= 0)
        {
            _position++;
        }

        return num;
    }

    public override void Flush()
    {
        _stream.Flush();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stream.Dispose();
        }

        base.Dispose(disposing);
    }
}
