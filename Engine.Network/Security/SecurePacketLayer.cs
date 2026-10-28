using System.Collections.Concurrent;
using System.Net;
using System.Text;

using Engine.Network.Transport;

using LiteNetLib.Layers;

using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

using Rendezvous.Protocol;

namespace Engine.Network.Security;

/// <summary>DTLS below LiteNetLib, outside application handshakes, delivery headers and ACKs.</summary>
internal sealed class SecurePacketLayer : PacketLayerBase, IDisposable
{
    private const int _envelopeSize = 5;

    private const byte _unconnectedProperty = 8;

    private const byte _broadcastProperty = 11;

    internal const int maximumRecordSize = 1200 - RelayDatagramCodec.Overhead - _envelopeSize;

    private readonly RelayPacketLayer _relay;

    private readonly NetworkIdentity _identity;

    private readonly Func<NetworkPeerIdentity, bool>? _validator;

    private readonly ConcurrentDictionary<IPEndPoint, SecureSession> _sessions = new();

    private readonly Lock _creationGate = new();

    private readonly Lock _verifierGate = new();

    private readonly int _maximumSessions;

    private readonly int _maximumHandshakes;

    private readonly long _setupLifetime;

    private DtlsVerifier _verifier = new(new BcTlsCrypto(new SecureRandom()));

    private long _verifierCreated = Environment.TickCount64;

    private bool _disposed;

    internal Action<IPEndPoint, byte[]>? send;

    internal SecurePacketLayer(RelayPacketLayer relay, NetworkHostOptions options)
        : base(RelayDatagramCodec.Overhead + _envelopeSize + SecureTlsPeer.recordBudget)
    {
        _relay = relay;
        _validator = options.ValidatePeerIdentity;
        _maximumSessions = options.MaxSecureSessions;
        _maximumHandshakes = options.MaxPendingSecureHandshakes;
        _setupLifetime = (long)options.RequestTimeout.TotalMilliseconds + 7000;
        ArgumentOutOfRangeException.ThrowIfLessThan(_maximumSessions, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(_maximumHandshakes, 1);
        _identity = options.Identity is { } supplied ? Copy(supplied) : NetworkIdentity.Create();
    }

    private static NetworkIdentity Copy(NetworkIdentity identity)
    {
        var bytes = identity.ExportPrivateKey();
        try { return NetworkIdentity.Import(bytes); }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
    }

    internal NetworkPeerIdentity LocalIdentity => _identity.PublicIdentity;

    internal int SessionCount => _sessions.Count;

    internal int PendingCount => _sessions.Count(pair => !pair.Value.IsReady);

    internal SecureSession GetReady(IPEndPoint endpoint)
    {
        return _sessions.TryGetValue(endpoint, out var session) && session.IsReady
            ? session
            : throw new IOException("Connected UDP traffic requires an authenticated DTLS session.");
    }

    internal async Task EstablishAsync(IPEndPoint endpoint, CancellationToken cancellationToken)
    {
        SecureSession session;
        lock (_creationGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_sessions.TryGetValue(endpoint, out session!))
            {
                CheckCapacity();
                session = Create(endpoint, true);
                _sessions.TryAdd(session.Endpoint, session);
                session.Start();
            }
        }

        try { await session.Ready.WaitAsync(TimeSpan.FromSeconds(6), cancellationToken).ConfigureAwait(false); }
        catch
        {
            Remove(session.Endpoint, session);
            throw;
        }
    }

    private void CheckCapacity()
    {
        if (_sessions.Count >= _maximumSessions || _sessions.Count(pair => !pair.Value.IsReady) >= _maximumHandshakes)
        {
            throw new IOException("Secure UDP session capacity reached.");
        }
    }

    private SecureSession Create(IPEndPoint endpoint, bool client, DtlsRequest? request = null)
    {
        return new(endpoint, _identity, _validator, record => SendRecord(endpoint, record), client, request);
    }

    private void SendRecord(IPEndPoint endpoint, byte[] record)
    {
        var payload = new byte[record.Length + 4];
        "SCDT"u8.CopyTo(payload);
        record.CopyTo(payload, 4);
        send?.Invoke(endpoint, payload);
    }

    public override void ProcessInboundPacket(IPEndPoint endpoint, ref byte[] data, ref int offset, ref int length)
    {
        _relay.ProcessInboundPacket(endpoint, ref data, ref offset, ref length);
        if (length == 0) { return; }

        var packet = data.AsSpan(offset, length);
        if (IsEnvelope(packet))
        {
            var record = packet[_envelopeSize..];
            if (record.Length > maximumRecordSize)
            {
                length = 0;
                return;
            }

            var decryptedLength = 0;
            try
            {
                if (_sessions.TryGetValue(endpoint, out var existing))
                {
                    decryptedLength = existing.Receive(data, offset, length);
                }
                else
                {
                    AcceptHandshake(endpoint, record);
                }
            }
            catch (IOException) { }
            catch (ArgumentException) { }

            if (decryptedLength == 0)
            {
                length = 0;
                return;
            }

            // Inner unconnected messages are not used by DTLS. Discovery must stay on its
            // explicit public path so an untrusted payload cannot change security classification.
            if (IsPublic(data.AsSpan(offset, decryptedLength)))
            {
                length = 0;
                return;
            }

            length = decryptedLength;
            return;
        }

        // Only discovery/signaling is allowed outside DTLS. Never accept plaintext Connect,
        // Disconnect, ACK or game payload packets, including while a secure session exists.
        if (!IsPublic(packet)) { length = 0; }
    }

    private void AcceptHandshake(IPEndPoint endpoint, ReadOnlySpan<byte> record)
    {
        if (record.Length > maximumRecordSize) { return; }

        lock (_creationGate)
        {
            if (_disposed || _sessions.ContainsKey(endpoint)) { return; }

            if (_sessions.Count >= _maximumSessions ||
                _sessions.Count(pair => !pair.Value.IsReady) >= _maximumHandshakes)
            {
                return;
            }

            DtlsRequest? request;
            lock (_verifierGate)
            {
                if (Environment.TickCount64 - _verifierCreated > 120000)
                {
                    _verifier = new DtlsVerifier(new BcTlsCrypto(new SecureRandom()));
                    _verifierCreated = Environment.TickCount64;
                }

                var bytes = record.ToArray();
                request = _verifier.VerifyRequest(
                    Encoding.UTF8.GetBytes(endpoint.ToString()),
                    bytes,
                    0,
                    bytes.Length,
                    new CookieSender(record => SendRecord(endpoint, record))
                );
            }

            // Stateless cookie verification prevents an unauthenticated spoofed ClientHello
            // from allocating a session, a certificate handshake, or a dedicated worker.
            if (request is null) { return; }

            var session = Create(endpoint, false, request);
            _sessions.TryAdd(session.Endpoint, session);
            session.Start();
        }
    }

    public override void ProcessOutBoundPacket(IPEndPoint endpoint, ref byte[] data, ref int offset, ref int length)
    {
        var packet = data.AsSpan(offset, length);
        // BroadcastDatagram uses the length-aware unconnected send path on the existing socket.
        if (endpoint.Address.Equals(IPAddress.Broadcast) && !packet.IsEmpty && packet[0] == _unconnectedProperty)
        {
            data[offset] = _broadcastProperty;
        }

        if (!IsPublic(packet))
        {
            if (!_sessions.TryGetValue(endpoint, out var session) || !session.IsReady || session.IsFailed)
            {
                length = 0;
                return;
            }

            int recordLength;
            try { recordLength = session.Encrypt(data, offset, length); }
            catch (IOException)
            {
                length = 0;
                return;
            }

            data[offset] = _unconnectedProperty;
            "SCDT"u8.CopyTo(data.AsSpan(offset + 1));
            length = recordLength + _envelopeSize;
        }

        _relay.ProcessOutBoundPacket(endpoint, ref data, ref offset, ref length);
    }

    private static bool IsEnvelope(ReadOnlySpan<byte> packet)
    {
        return packet.Length >= _envelopeSize &&
               packet[0] == _unconnectedProperty &&
               packet.Slice(1, 4).SequenceEqual("SCDT"u8);
    }

    private static bool IsPublic(ReadOnlySpan<byte> packet)
    {
        return !packet.IsEmpty && (packet[0] & 31) is _unconnectedProperty or _broadcastProperty;
    }

    internal void Sweep()
    {
        foreach (var pair in _sessions)
        {
            if (pair.Value.IsFailed ||
                !pair.Value.IsReady && Environment.TickCount64 - pair.Value.StartedAt > 6000 ||
                !pair.Value.attached && Environment.TickCount64 - pair.Value.StartedAt > _setupLifetime)
            {
                Remove(pair.Key, pair.Value);
            }
        }
    }

    internal void Remove(IPEndPoint endpoint)
    {
        if (_sessions.TryGetValue(endpoint, out var session)) { Remove(endpoint, session); }
    }

    private void Remove(IPEndPoint endpoint, SecureSession session)
    {
        if (_sessions.TryRemove(new KeyValuePair<IPEndPoint, SecureSession>(endpoint, session))) { session.Dispose(); }
    }

    public void Dispose()
    {
        lock (_creationGate)
        {
            if (_disposed) { return; }

            _disposed = true;
            foreach (var pair in _sessions) { Remove(pair.Key, pair.Value); }

            _identity.Dispose();
        }
    }

    private sealed class CookieSender(Action<byte[]> send) : DatagramSender
    {
        public int GetSendLimit() => maximumRecordSize;

        public void Send(byte[] buffer, int offset, int length) => send(buffer.AsSpan(offset, length).ToArray());

        public void Send(ReadOnlySpan<byte> buffer) => send(buffer.ToArray());
    }
}
