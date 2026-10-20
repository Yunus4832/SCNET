using Engine.Network;

using Game.Network;
using Game.Network.Handling;

namespace Game.Servers;

/// <summary>Owns the socket and prepared path used for server inspection and the subsequent game handshake.</summary>
public sealed class ServerConnectionPreparation : IAsyncDisposable
{
    private readonly NetworkHost _host;
    private readonly ConnectionPreparation _path;
    private readonly Lock _ownershipGate = new();
    private readonly CancellationTokenSource _leaseStopping = new();
    private readonly Task _leaseMaintaining;
    private Exception? _leaseFailure;
    private bool _consumed;
    private bool _disposed;

    private ServerConnectionPreparation(NetworkHost host, ConnectionPreparation path, ServerRuntimeStatus status)
    {
        _host = host;
        _path = path;
        Target = path.Target ?? throw new InvalidOperationException("Prepared path has no logical target.");
        Status = status;
        _leaseMaintaining = Task.Run(MaintainLeaseAsync);
    }

    public NetworkTarget Target { get; }

    public ServerRuntimeStatus Status { get; }

    public DateTimeOffset ExpiresAt => _path.ExpiresAt;

    public static async Task<ServerConnectionPreparation> PrepareAsync(NetworkTarget target, HttpClient http,
        CancellationToken cancellationToken)
    {
        var host = new NetworkHost(options: new NetworkHostOptions
        {
            DisconnectTimeout = TimeSpan.FromMilliseconds(CommonLib.DisconnectTimeout)
        });
        ConnectionPreparation? path = null;
        try
        {
            path = await host.PrepareConnectionAsync(target, http, cancellationToken).ConfigureAwait(false);
            ServerRuntimeStatus? status = null;
            var stopwatch = Stopwatch.StartNew();
            void Receive(byte[] payload)
            {
                var package = PackageManager.DecodePackage<ServerInfoPackage>(null, payload);
                if (!package.Package.RequestInfo)
                {
                    status = ServerRuntimeStatus.FromPackage(package.Package, stopwatch.ElapsedMilliseconds);
                }
            }
            path.DatagramReceived += Receive;
            try
            {
                void SendProbe() => path.SendDatagram(NetNode.CreateUnconnectedPayload(new ServerInfoPackage(true)));
                SendProbe();
                await ServerDiscoveryService.PollAsync(host, TimeSpan.FromSeconds(2), cancellationToken,
                    () => status is not null, SendProbe).ConfigureAwait(false);
            }
            finally
            {
                path.DatagramReceived -= Receive;
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (status is null)
            {
                throw new TimeoutException("The server did not answer the connection preparation probe.");
            }
            return new ServerConnectionPreparation(host, path, status);
        }
        catch
        {
            try
            {
                if (path is not null)
                {
                    await path.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                Log.Warning($"Could not release failed server preparation: {exception.Message}");
            }
            finally
            {
                await host.DisposeAsync().ConfigureAwait(false);
            }
            throw;
        }
    }

    private async Task MaintainLeaseAsync()
    {
        try
        {
            while (!_leaseStopping.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(20), _leaseStopping.Token).ConfigureAwait(false);
                await _path.ExtendLeaseAsync(TimeSpan.FromMinutes(2), _leaseStopping.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_leaseStopping.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _leaseFailure = exception;
        }
    }

    internal (NetworkHost Host, ConnectionPreparation Path) Consume()
    {
        lock (_ownershipGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_consumed)
            {
                throw new InvalidOperationException("Server preparation has already been consumed.");
            }
            _leaseStopping.Cancel();
            _leaseMaintaining.GetAwaiter().GetResult();
            if (_leaseFailure is not null)
            {
                throw new IOException("The prepared server path could not be renewed.", _leaseFailure);
            }
            _consumed = true;
            _leaseStopping.Dispose();
            return (_host, _path);
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_ownershipGate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            if (_consumed)
            {
                return;
            }
        }
        await _leaseStopping.CancelAsync().ConfigureAwait(false);
        await _leaseMaintaining.ConfigureAwait(false);
        _leaseStopping.Dispose();
        try
        {
            await _path.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await _host.DisposeAsync().ConfigureAwait(false);
        }
    }
}
