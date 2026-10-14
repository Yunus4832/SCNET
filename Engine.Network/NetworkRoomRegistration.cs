using Rendezvous.Protocol;

namespace Engine.Network;

/// <summary>Maintains an optional room registration without owning the game listener or its connections.</summary>
public sealed class NetworkRoomRegistration : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<DateTimeOffset>> _renew;
    private readonly Func<CancellationToken, Task> _close;
    private readonly Func<CancellationToken, Task>? _receiveOffers;
    private readonly Func<RoomPublication, CancellationToken, Task> _updateRoom;
    private readonly Action<NetworkRoomRegistration, Exception> _failed;
    private readonly CancellationTokenSource _lifetime;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Task _maintenance;
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposed;
    private Exception? _failure;
    private long _expiresAtTicks;

    internal NetworkRoomRegistration(Guid sessionId, string joinCode, DateTimeOffset expiresAt, CancellationToken hostLifetime,
        Func<CancellationToken, Task<DateTimeOffset>> renew, Func<CancellationToken, Task> close,
        Action<NetworkRoomRegistration, Exception> failed, Func<RoomPublication, CancellationToken, Task> updateRoom,
        Func<CancellationToken, Task>? receiveOffers = null)
    {
        SessionId = sessionId;
        JoinCode = joinCode;
        _expiresAtTicks = expiresAt.UtcTicks;
        _renew = renew;
        _close = close;
        _failed = failed;
        _receiveOffers = receiveOffers;
        _updateRoom = updateRoom;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(hostLifetime);
        _maintenance = Task.Run(() => Task.WhenAll(MaintainAsync(), ReceiveOffersAsync()));
    }

    public Guid SessionId { get; }

    public string JoinCode { get; }

    public DateTimeOffset ExpiresAt => new(Interlocked.Read(ref _expiresAtTicks), TimeSpan.Zero);

    public Exception? Failure => Volatile.Read(ref _failure);

    public bool IsActive => Volatile.Read(ref _disposed) == 0 && Failure is null && ExpiresAt > DateTimeOffset.UtcNow;

    public Task Maintenance => _maintenance;

    public async Task RenewAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        await _gate.WaitAsync(deadline.Token).ConfigureAwait(false);
        try
        {
            var expiresAt = await _renew(deadline.Token).ConfigureAwait(false);
            Interlocked.Exchange(ref _expiresAtTicks, expiresAt.UtcTicks);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateRoomAsync(RoomPublication publication, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publication);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        await _gate.WaitAsync(deadline.Token).ConfigureAwait(false);
        try
        {
            deadline.Token.ThrowIfCancellationRequested();
            await _updateRoom(publication, deadline.Token).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task MaintainAsync()
    {
        try
        {
            while (!_lifetime.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(20), _lifetime.Token).ConfigureAwait(false);
                await RenewAsync(_lifetime.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Volatile.Write(ref _failure, exception);
            _failed(this, exception);
            await _lifetime.CancelAsync().ConfigureAwait(false);
        }
    }

    private async Task ReceiveOffersAsync()
    {
        if (_receiveOffers is null)
        {
            return;
        }
        try
        {
            while (!_lifetime.IsCancellationRequested)
            {
                await _receiveOffers(_lifetime.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Volatile.Write(ref _failure, exception);
            _failed(this, exception);
            await _lifetime.CancelAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            await _closed.Task.ConfigureAwait(false);
            return;
        }
        try
        {
            await _lifetime.CancelAsync().ConfigureAwait(false);
            await _maintenance.ConfigureAwait(false);
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await _close(deadline.Token).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
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
