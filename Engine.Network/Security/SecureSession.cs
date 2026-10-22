using System.Net;
using System.Security.Cryptography;

using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace Engine.Network.Security;

internal sealed class SecureSession : IDisposable
{
    private readonly Lock _gate = new();

    private readonly SecureDatagramTransport _raw;

    private readonly BcTlsCrypto _crypto = new(new SecureRandom());

    private Task _worker = Task.CompletedTask;

    private readonly NetworkIdentity _identity;

    private readonly Func<NetworkPeerIdentity, bool>? _validator;

    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly bool _client;

    private readonly DtlsRequest? _request;

    private DtlsTransport? _dtls;

    private NetworkPeerIdentity? _peer;

    private byte[] _binding = [];

    private bool _disposed;

    private volatile bool _failed;

    internal volatile bool attached;

    internal SecureSession(
        IPEndPoint endpoint,
        NetworkIdentity identity,
        Func<NetworkPeerIdentity, bool>? validator,
        Action<byte[]> send,
        bool client,
        DtlsRequest? request = null
    )
    {
        Endpoint = new IPEndPoint(endpoint.Address, endpoint.Port);
        _identity = identity;
        _validator = validator;
        _raw = new SecureDatagramTransport(send, SecurePacketLayer.maximumRecordSize);
        _client = client;
        _request = request;
        StartedAt = Environment.TickCount64;
    }

    internal IPEndPoint Endpoint { get; }

    internal long StartedAt { get; }

    internal bool IsReady => _ready.Task.IsCompletedSuccessfully;

    internal bool IsFailed => _failed || _ready.Task.IsFaulted || _ready.Task.IsCanceled;

    internal Task Ready => _ready.Task;

    internal NetworkPeerIdentity PeerIdentity => _peer ?? throw new InvalidOperationException("Handshake incomplete.");

    internal ReadOnlyMemory<byte> ChannelBinding => _binding;

    internal void Start()
    {
        // The DTLS library performs a blocking handshake. Bound these dedicated workers at the host,
        // rather than allowing idle handshakes to exhaust the shared thread pool and stop network pumps.
        _worker = Task.Factory.StartNew(
            Handshake,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );
    }

    private void Handshake()
    {
        try
        {
            var transport = _client
                ? new DtlsClientProtocol().Connect(
                    new SecureTlsPeer.Client(_identity, _crypto, Verify, SetBinding),
                    _raw
                )
                : new DtlsServerProtocol().Accept(
                    new SecureTlsPeer.Server(_identity, _crypto, Verify, SetBinding),
                    _raw,
                    _request
                );
            lock (_gate)
            {
                if (_disposed)
                {
                    transport.Close();
                    return;
                }

                if (_peer is null || _binding.Length != 32)
                {
                    throw new IOException("DTLS did not authenticate a peer and export a binding.");
                }

                // No LiteNetLib connection exists yet. An early ConnectRequest can safely be retried
                // by LiteNetLib; do not leave it queued behind the first post-handshake datagram.
                _raw.Clear();
                _dtls = transport;
                _ready.TrySetResult();
            }
        }
        catch (Exception exception)
        {
            _failed = true;
            _ready.TrySetException(new IOException("Secure UDP handshake failed.", exception));
            _raw.Close();
        }
    }

    private void Verify(NetworkPeerIdentity identity)
    {
        if (_validator is not null && !_validator(identity))
        {
            throw new TlsFatalAlert(AlertDescription.bad_certificate);
        }

        _peer = identity;
    }

    private void SetBinding(byte[] binding) => _binding = binding;

    internal int Encrypt(byte[] packet, int offset, int length)
    {
        lock (_gate)
        {
            if (_disposed || _dtls is null) { throw new IOException("Secure UDP session unavailable."); }

            if (length > _dtls.GetSendLimit()) { throw new IOException("Secure UDP record too large."); }

            _raw.capture = packet;
            _raw.captureOffset = offset + 5;
            _raw.capturedRecords = 0;
            try { _dtls.Send(packet.AsSpan(offset, length)); }
            finally { _raw.capture = null; }

            if (_raw.capturedRecords != 1)
            {
                throw new IOException("Expected one DTLS record per transport datagram.");
            }

            return _raw.capturedLength;
        }
    }

    internal int Receive(byte[] packet, int offset, int length)
    {
        lock (_gate)
        {
            if (_disposed) { return 0; }

            if (_dtls is null)
            {
                _raw.Enqueue(packet.AsSpan(offset + 5, length - 5));
                return 0;
            }

            _raw.current = packet.AsMemory(offset + 5, length - 5);
            try { return Math.Max(0, _dtls.Receive(packet, offset, packet.Length - offset, 1)); }
            catch (IOException)
            {
                _failed = true;
                throw;
            }
            finally { _raw.current = default; }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) { return; }

            _disposed = true;
            _ready.TrySetCanceled();
            _ = _ready.Task.Exception;
            try { _dtls?.Close(); }
            catch (IOException) { }
            finally
            {
                _raw.Close();
                CryptographicOperations.ZeroMemory(_binding);
            }
        }

        _worker.GetAwaiter().GetResult();
    }
}
