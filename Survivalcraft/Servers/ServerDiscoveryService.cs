using System.Net.Sockets;

using Engine.Network;

using Game.Network;
using Game.Network.Handling;

namespace Game.Servers;

public sealed class ServerDiscoveryService
{
    public Task<ServerRuntimeStatus> ProbeAsync(string address, TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => Probe(address, timeout, cancellationToken), cancellationToken);
    }

    public Task<IReadOnlyList<ServerItem>> DiscoverLanAsync(TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => DiscoverLan(timeout, cancellationToken), cancellationToken);
    }

    private static async Task<ServerRuntimeStatus> Probe(string address, TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!CommonLib.Resolve(address, out var endpoint))
        {
            return new ServerRuntimeStatus { Availability = ServerAvailability.Unavailable };
        }

        await using var network = new NetworkHost(options: new NetworkHostOptions { ReuseAddress = true });
        ServerRuntimeStatus? status = null;
        try
        {
            var stopwatch = Stopwatch.StartNew();
            network.DatagramReceived += (source, payload) =>
            {
                var remote = source.TransportEndpoint;
                if (!remote.Equals(endpoint))
                {
                    return;
                }

                var package = PackageManager.DecodePackage<ServerInfoPackage>(null, payload, new Client(remote));
                status = ServerRuntimeStatus.FromPackage(package.Package, stopwatch.ElapsedMilliseconds);
            };
            void SendProbe() => network.SendDatagram(endpoint!, NetNode.CreateUnconnectedPayload(new ServerInfoPackage(true)));
            SendProbe();
            await PollAsync(network, timeout, cancellationToken, () => status is not null, SendProbe).ConfigureAwait(false);
            return status ?? new ServerRuntimeStatus { Availability = ServerAvailability.Unavailable };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Log.Warning($"Server probe failed for '{address}': {exception.Message}");
            return new ServerRuntimeStatus { Availability = ServerAvailability.Unavailable };
        }
    }

    private static async Task<IReadOnlyList<ServerItem>> DiscoverLan(TimeSpan timeout, CancellationToken cancellationToken)
    {
        await using var network = new NetworkHost(options: new NetworkHostOptions { ReuseAddress = true });
        var items = new Dictionary<string, ServerItem>(StringComparer.Ordinal);
        try
        {
            var stopwatch = Stopwatch.StartNew();
            network.DatagramReceived += (source, payload) =>
            {
                var remote = source.TransportEndpoint;
                if (remote.Address.AddressFamily != AddressFamily.InterNetwork)
                {
                    return;
                }

                try
                {
                    var package = PackageManager.DecodePackage<ServerInfoPackage>(null, payload, new Client(remote));
                    var address = remote.ToString();
                    items[address] = new ServerItem
                    {
                        SourceId = ServerSourceIds.Lan,
                        EntryId = address,
                        SourceKind = ServerSourceKind.Lan,
                        SourceName = "LAN",
                        Address = address,
                        DisplayName = address,
                        Order = items.Count,
                        RuntimeStatus = ServerRuntimeStatus.FromPackage(package.Package, stopwatch.ElapsedMilliseconds)
                    };
                }
                catch (Exception exception)
                {
                    Log.Warning($"Ignored invalid LAN discovery response from '{remote}': {exception.Message}");
                }
            };
            network.BroadcastDatagram(NetNode.CreateUnconnectedPayload(new ServerInfoPackage(true)), SettingsManager.Current.BroadcastPort);
            await PollAsync(network, timeout, cancellationToken, () => false).ConfigureAwait(false);
            return items.Values.ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Log.Warning($"LAN discovery failed: {exception.Message}");
            return [];
        }
    }

    internal static async Task PollAsync(NetworkHost network, TimeSpan timeout, CancellationToken cancellationToken,
        Func<bool> completed, Action? retry = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var nextRetry = TimeSpan.FromMilliseconds(250);
        while (stopwatch.Elapsed < timeout && !completed())
        {
            cancellationToken.ThrowIfCancellationRequested();
            network.DispatchEvents();
            if (retry is not null && !completed() && stopwatch.Elapsed < timeout && stopwatch.Elapsed >= nextRetry)
            {
                cancellationToken.ThrowIfCancellationRequested();
                retry();
                nextRetry = stopwatch.Elapsed + TimeSpan.FromMilliseconds(250);
            }

            await Task.Delay(2, cancellationToken).ConfigureAwait(false);
        }
    }

}
