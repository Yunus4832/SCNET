using System.Net;
using System.Net.Sockets;

using Microsoft.Extensions.Logging.Abstractions;

using Rendezvous.Protocol;

using RendezvousServer.Sessions;

namespace RendezvousServer.Relay;

/// <summary>Allocates bounded dedicated relay ports and retains established paths independently of invitations.</summary>
public sealed class RelayRouteRegistry(
    InvitationRegistry invitations,
    TimeProvider clock,
    IPAddress bindAddress,
    IPAddress advertisedAddress,
    int firstPort,
    int portCount,
    int lifetimeSeconds = 60,
    int bytesPerSecond = 1024 * 1024,
    ILoggerFactory? loggerFactory = null
) : IAsyncDisposable
{
    private readonly ILogger<RelayRouteRegistry> _logger =
        loggerFactory?.CreateLogger<RelayRouteRegistry>() ?? NullLogger<RelayRouteRegistry>.Instance;

    private readonly int _capacity = ValidatePorts(firstPort, portCount);

    private readonly Lock _gate = new();

    private readonly Dictionary<(Guid Session, Guid Participant, Guid Attempt), Entry> _routes = new();

    private bool _disposed;

    public RelayAllocation Allocate(Guid sessionId, Guid participantId, Guid attemptId, string credential)
    {
        if (attemptId == Guid.Empty ||
            participantId == Guid.Empty ||
            !invitations.Authenticate(sessionId, participantId, credential))
        {
            throw new InvalidOperationException("relay_unauthorized");
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var key = (sessionId, participantId, attemptId);
            if (_routes.TryGetValue(key, out var existing))
            {
                if (existing.Route.Completion.IsCompleted ||
                    existing.Route.LastActivity + TimeSpan.FromSeconds(lifetimeSeconds) <= clock.GetUtcNow())
                {
                    throw new InvalidOperationException("relay_expired");
                }

                return Allocation(key, existing, 1);
            }

            if (_routes.Count >= _capacity)
            {
                throw new InvalidOperationException("relay_capacity_reached");
            }

            for (var port = firstPort; port < firstPort + _capacity; port++)
            {
                if (_routes.Values.Any(entry => entry.Route.Endpoint.Port == port))
                {
                    continue;
                }

                UdpRelayRoute route;
                try
                {
                    route = new UdpRelayRoute(
                        new IPEndPoint(bindAddress, port),
                        clock,
                        bytesPerSecond,
                        loggerFactory?.CreateLogger<UdpRelayRoute>()
                    );
                }
                catch (SocketException)
                {
                    continue;
                }

                var entry = new Entry(route, clock.GetUtcNow() + TimeSpan.FromSeconds(lifetimeSeconds));
                _routes.Add(key, entry);
                return Allocation(key, entry, 1);
            }

            throw new InvalidOperationException("relay_capacity_reached");
        }
    }

    public RelayAllocation RenewPreparation(Guid sessionId, Guid participantId, Guid attemptId, string credential)
    {
        if (!invitations.Authenticate(sessionId, participantId, credential))
        {
            throw new InvalidOperationException("relay_unauthorized");
        }

        lock (_gate)
        {
            var key = (sessionId, participantId, attemptId);
            if (!_routes.TryGetValue(key, out var entry) ||
                entry.ExpiresAt <= clock.GetUtcNow() ||
                entry.Route.Completion.IsCompleted ||
                entry.Route.LastActivity + TimeSpan.FromSeconds(lifetimeSeconds) <= clock.GetUtcNow())
            {
                throw new InvalidOperationException("relay_expired");
            }

            entry.ExpiresAt = clock.GetUtcNow() + TimeSpan.FromSeconds(lifetimeSeconds);
            entry.HostAcknowledged = false;
            return Allocation(key, entry, 1);
        }
    }

    public IReadOnlyList<RelayAllocation> GetHostOffers(Guid sessionId, string credential)
    {
        if (!invitations.Authenticate(sessionId, Guid.Empty, credential))
        {
            throw new InvalidOperationException("relay_unauthorized");
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _routes.Where(item => item.Key.Session == sessionId &&
                                         !item.Value.HostAcknowledged &&
                                         item.Value.ExpiresAt > clock.GetUtcNow() &&
                                         !item.Value.Route.Completion.IsCompleted
                )
                .Select(item => Allocation(item.Key, item.Value, 0)).ToArray();
        }
    }

    public void AcknowledgeHost(
        Guid sessionId,
        Guid participantId,
        Guid attemptId,
        string credential,
        DateTimeOffset expiresAt
    )
    {
        if (!invitations.Authenticate(sessionId, Guid.Empty, credential))
        {
            throw new InvalidOperationException("relay_unauthorized");
        }

        lock (_gate)
        {
            if (!_routes.TryGetValue((sessionId, participantId, attemptId), out var entry))
            {
                throw new InvalidOperationException("relay_not_found");
            }

            if (entry.ExpiresAt == expiresAt)
            {
                entry.HostAcknowledged = true;
            }
        }
    }

    public async Task CleanExpiredAsync()
    {
        Entry[] expired;
        lock (_gate)
        {
            var keys = _routes.Where(item => item.Value.Route.Completion.IsCompleted ||
                                             item.Value.Route.LastActivity + TimeSpan.FromSeconds(lifetimeSeconds) <=
                                             clock.GetUtcNow()
                )
                .Select(item => item.Key).ToArray();
            expired = keys.Select(key => _routes[key]).ToArray();
            foreach (var key in keys)
            {
                _routes.Remove(key);
            }
        }

        await Task.WhenAll(expired.Select(entry => DisposeRouteAsync(entry.Route)));
    }

    private async Task DisposeRouteAsync(UdpRelayRoute route)
    {
        try
        {
            await route.DisposeAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Could not dispose relay route {RouteId} at {Endpoint}",
                route.Id,
                route.Endpoint
            );
        }
    }

    private RelayAllocation Allocation((Guid Session, Guid Participant, Guid Attempt) key, Entry entry, byte side) =>
        new(
            key.Session,
            key.Participant,
            key.Attempt,
            new IPEndPoint(advertisedAddress, entry.Route.Endpoint.Port).ToString(),
            entry.Route.GetCredentials(side),
            entry.ExpiresAt
        );

    public async ValueTask DisposeAsync()
    {
        Entry[] entries;
        lock (_gate)
        {
            _disposed = true;
            entries = _routes.Values.ToArray();
            _routes.Clear();
        }

        await Task.WhenAll(entries.Select(entry => DisposeRouteAsync(entry.Route)));
    }

    private static int ValidatePorts(int firstPort, int portCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(firstPort, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(portCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(portCount, 256);
        if (firstPort + portCount > 65536 || (firstPort == 0 && portCount != 1))
        {
            throw new ArgumentOutOfRangeException(nameof(firstPort));
        }

        return portCount;
    }

    private sealed class Entry(UdpRelayRoute route, DateTimeOffset expiresAt)
    {
        public UdpRelayRoute Route { get; } = route;

        public DateTimeOffset ExpiresAt { get; set; } = expiresAt;

        public bool HostAcknowledged { get; set; }
    }
}
