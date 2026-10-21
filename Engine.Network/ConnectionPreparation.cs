using System.Net;

namespace Engine.Network;

/// <summary>Holds a selected path on the host socket until it is connected or released.</summary>
public sealed class ConnectionPreparation : IAsyncDisposable
{
    private readonly NetworkHost _host;

    private readonly IPEndPoint _endpoint;

    private readonly CancellationTokenSource _lifetime = new();

    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly Action? _releasePath;

    private readonly TimeProvider _clock;

    private Func<CancellationToken, Task>? _releaseRegistration;

    private Func<TimeSpan, CancellationToken, Task<DateTimeOffset>>? _renewLease;

    private long _expiresAtTicks;

    private int _started;

    private int _disposed;

    private bool _transferred;

    internal ConnectionPreparation(
        NetworkHost host,
        IPEndPoint endpoint,
        NetworkPathKind pathKind,
        DateTimeOffset expiresAt,
        Action? releasePath,
        TimeProvider clock
    )
    {
        _host = host;
        _endpoint = endpoint;
        PathKind = pathKind;
        _expiresAtTicks = expiresAt.UtcTicks;
        _releasePath = releasePath;
        _clock = clock;
        _host.DatagramReceived += OnDatagram;
    }

    public NetworkTarget? Target { get; internal set; }

    public NetworkPathKind PathKind { get; }

    public DateTimeOffset ExpiresAt => new(Interlocked.Read(ref _expiresAtTicks), TimeSpan.Zero);

    public event Action<byte[]>? DatagramReceived;

    public void SendDatagram(ReadOnlySpan<byte> payload)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_clock.GetUtcNow() >= ExpiresAt)
        {
            throw new TimeoutException("Connection preparation expired.");
        }

        if (Volatile.Read(ref _started) != 0)
        {
            throw new InvalidOperationException("Preparation has already been consumed.");
        }

        _host.SendDatagram(_endpoint, payload);
    }

    private void OnDatagram(NetworkDatagramEndpoint source, byte[] payload)
    {
        if (Volatile.Read(ref _disposed) == 0 && Volatile.Read(ref _started) == 0 && source.Endpoint.Equals(_endpoint))
        {
            DatagramReceived?.Invoke(payload);
        }
    }

    public async Task<NetworkConnection> ConnectAsync(
        ReadOnlyMemory<byte> handshake,
        CancellationToken cancellationToken
    )
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var remaining = ExpiresAt - _clock.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            throw new TimeoutException("Connection preparation expired.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        deadline.CancelAfter(remaining);
        try
        {
            await _gate.WaitAsync(deadline.Token).ConfigureAwait(false);
            try
            {
                if (Interlocked.Exchange(ref _started, 1) != 0)
                {
                    throw new InvalidOperationException("Preparation has already been consumed.");
                }

                var connection = await _host.ConnectAsync(_endpoint, handshake, deadline.Token).ConfigureAwait(false);
                if (deadline.IsCancellationRequested)
                {
                    connection.Disconnect();
                    deadline.Token.ThrowIfCancellationRequested();
                }

                connection.PathKind = PathKind;
                _host.DatagramReceived -= OnDatagram;
                _transferred = true;
                if (PathKind != NetworkPathKind.Relay)
                {
                    _releasePath?.Invoke();
                }

                return connection;
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested &&
                                                           !_lifetime.IsCancellationRequested)
        {
            throw new TimeoutException("The prepared connection handshake timed out.", exception);
        }
    }

    public async Task ExtendLeaseAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromMinutes(2))
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        await _gate.WaitAsync(deadline.Token).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _started) != 0)
            {
                throw new InvalidOperationException("Preparation has already been consumed.");
            }

            if (_clock.GetUtcNow() >= ExpiresAt)
            {
                throw new TimeoutException("Connection preparation expired.");
            }

            var expiresAt = _renewLease is null
                ? _clock.GetUtcNow() + duration
                : await _renewLease(duration, deadline.Token).ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();
            if (expiresAt <= _clock.GetUtcNow())
            {
                throw new TimeoutException("Connection preparation renewal expired.");
            }

            Interlocked.Exchange(ref _expiresAtTicks, expiresAt.UtcTicks);
        }
        finally
        {
            _gate.Release();
        }
    }

    internal void SetLeaseRenewal(Func<TimeSpan, CancellationToken, Task<DateTimeOffset>> renew) => _renewLease = renew;

    internal void SetRegistrationRelease(Func<CancellationToken, Task> release) => _releaseRegistration = release;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            await _closed.Task.ConfigureAwait(false);
            return;
        }

        try
        {
            _host.DatagramReceived -= OnDatagram;
            await _lifetime.CancelAsync().ConfigureAwait(false);
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_transferred)
                {
                    _releasePath?.Invoke();
                }

                if (_releaseRegistration is not null)
                {
                    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    await _releaseRegistration(deadline.Token).ConfigureAwait(false);
                }
            }
            finally
            {
                _gate.Release();
                // The connected transport belongs to NetworkHost and survives disposal of the preparation.
                _lifetime.Dispose();
            }

            _closed.TrySetResult();
        }
        catch (Exception exception)
        {
            _closed.TrySetException(exception);
            throw;
        }
    }
}
