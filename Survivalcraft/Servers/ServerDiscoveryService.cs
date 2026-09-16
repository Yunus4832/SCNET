using System.Net;
using System.Net.Sockets;

using Game.Network;
using Game.Network.Packages;
using Game.Network.Serialization;

using LiteNetLib;

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

    private static ServerRuntimeStatus Probe(string address, TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!CommonLib.Resolve(address, out var endpoint))
        {
            return new ServerRuntimeStatus { Availability = ServerAvailability.Unavailable };
        }

        var listener = new EventBasedNetListener();
        var network = new NetManager(listener) { ReuseAddress = true, UnconnectedMessagesEnabled = true };
        ServerRuntimeStatus? status = null;
        try
        {
            network.Start();
            var stopwatch = Stopwatch.StartNew();
            listener.NetworkReceiveUnconnectedEvent += (remote, reader, _) =>
            {
                if (!remote.Equals(endpoint))
                {
                    return;
                }

                var package = PackageManager.DecodePackage<ServerInfoPackage>(null, reader, null, null, remote);
                status = CreateStatus(package, stopwatch.ElapsedMilliseconds);
            };
            NetNode.SendWriterFromPackage(network, [new ServerInfoPackage(true)], endpoint);
            Poll(network, timeout, cancellationToken, () => status is not null);
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
        finally
        {
            network.Stop();
        }
    }

    private static IReadOnlyList<ServerItem> DiscoverLan(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var listener = new EventBasedNetListener();
        var network = new NetManager(listener) { ReuseAddress = true, UnconnectedMessagesEnabled = true };
        var items = new Dictionary<string, ServerItem>(StringComparer.Ordinal);
        try
        {
            network.Start();
            var stopwatch = Stopwatch.StartNew();
            listener.NetworkReceiveUnconnectedEvent += (remote, reader, _) =>
            {
                if (remote.Address.AddressFamily != AddressFamily.InterNetwork)
                {
                    return;
                }

                try
                {
                    var package = PackageManager.DecodePackage<ServerInfoPackage>(null, reader, null, null, remote);
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
                        RuntimeStatus = CreateStatus(package, stopwatch.ElapsedMilliseconds)
                    };
                }
                catch (Exception exception)
                {
                    Log.Warning($"Ignored invalid LAN discovery response from '{remote}': {exception.Message}");
                }
            };
            NetNode.SendWriterFromPackage(network, [new ServerInfoPackage(true)], null);
            Poll(network, timeout, cancellationToken, () => false);
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
        finally
        {
            network.Stop();
        }
    }

    private static void Poll(NetManager network, TimeSpan timeout, CancellationToken cancellationToken,
        Func<bool> completed)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout && !completed())
        {
            cancellationToken.ThrowIfCancellationRequested();
            network.PollEvents();
            Thread.Sleep(2);
        }
    }

    private static ServerRuntimeStatus CreateStatus(ServerInfoPackage package, long pingMilliseconds)
    {
        return new ServerRuntimeStatus
        {
            Availability = ServerAvailability.Available,
            PingMilliseconds = pingMilliseconds,
            GameMode = package.GameMode,
            MaxPlayerCount = package.MaxPlayerCount,
            PlayerCount = package.ClientCount,
            Version = package.Version,
            TimeOfDay = package.TimeOfDay,
            TemporaryRepositories = package.TemporaryRepositories,
            RequiredModProfile = package.RequiredModProfile,
            Season = package.Season,
            TimeOfSeason = package.TimeOfSeason
        };
    }
}
