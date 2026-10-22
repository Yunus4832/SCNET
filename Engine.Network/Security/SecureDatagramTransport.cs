using System.Collections.Concurrent;

using Org.BouncyCastle.Tls;

namespace Engine.Network.Security;

/// <summary>Bounded handshake mailbox and synchronous DTLS record output capture.</summary>
internal sealed class SecureDatagramTransport(Action<byte[]> send, int maximumDatagram) : DatagramTransport
{
    private readonly BlockingCollection<byte[]> _incoming = new(64);

    private volatile bool _closed;

    internal byte[]? capture;

    internal int captureOffset;

    internal int capturedLength;

    internal int capturedRecords;

    internal ReadOnlyMemory<byte> current;

    internal bool Enqueue(ReadOnlySpan<byte> packet)
    {
        if (_closed || packet.Length > maximumDatagram) { return false; }

        try { return _incoming.TryAdd(packet.ToArray()); }
        catch (InvalidOperationException) { return false; }
    }

    internal void Clear()
    {
        while (_incoming.TryTake(out _)) { }
    }

    public int GetReceiveLimit() => maximumDatagram;

    public int GetSendLimit() => maximumDatagram;

    public int Receive(byte[] buffer, int offset, int length, int waitMillis)
    {
        if (_closed) { throw new IOException("Secure transport closed."); }

        if (!current.IsEmpty)
        {
            var count = current.Length;
            current.Span.CopyTo(buffer.AsSpan(offset, length));
            current = default;
            return count;
        }

        if (!_incoming.TryTake(out var packet, waitMillis)) { return -1; }

        if (packet.Length > length) { throw new IOException("DTLS receive buffer too small."); }

        packet.CopyTo(buffer, offset);
        return packet.Length;
    }

    public int Receive(Span<byte> buffer, int waitMillis)
    {
        if (_closed) { throw new IOException("Secure transport closed."); }

        if (!current.IsEmpty)
        {
            var count = current.Length;
            current.Span.CopyTo(buffer);
            current = default;
            return count;
        }

        if (!_incoming.TryTake(out var packet, waitMillis)) { return -1; }

        if (packet.Length > buffer.Length) { throw new IOException("DTLS receive buffer too small."); }

        packet.CopyTo(buffer);
        return packet.Length;
    }

    public void Send(byte[] buffer, int offset, int length) => Send(buffer.AsSpan(offset, length));

    public void Send(ReadOnlySpan<byte> buffer)
    {
        if (_closed) { throw new IOException("Secure transport closed."); }

        if (buffer.Length > maximumDatagram) { throw new IOException("DTLS datagram exceeds transport limit."); }

        if (capture is { } destination)
        {
            buffer.CopyTo(destination.AsSpan(captureOffset));
            capturedLength = buffer.Length;
            capturedRecords++;
        }
        else { send(buffer.ToArray()); }
    }

    public void Close()
    {
        _closed = true;
        _incoming.CompleteAdding();
    }
}
