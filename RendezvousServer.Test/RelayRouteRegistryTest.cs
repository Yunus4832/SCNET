using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

using Microsoft.Extensions.Logging;

using Rendezvous.Protocol;

using RendezvousServer.Relay;
using RendezvousServer.Sessions;

namespace RendezvousServer.Test;

public sealed class RelayRouteRegistryTest
{
    [Fact]
    public async Task AuthenticatedCloseAllowsReclaimingTheSamePoolPort()
    {
        int port;
        using (var reservation = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
        {
            port = ((IPEndPoint)reservation.Client.LocalEndPoint!).Port;
        }
        var clock = new ManualClock();
        var invitations = new InvitationRegistry(clock, TimeSpan.FromMinutes(5), 1);
        var invitation = invitations.Create(2);
        var participant = invitations.Join(invitation.JoinCode, Guid.NewGuid());
        await using var registry = new RelayRouteRegistry(invitations, clock, IPAddress.Loopback, IPAddress.Loopback, port, 1);
        var allocated = registry.Allocate(invitation.SessionId, participant.ParticipantId, Guid.NewGuid(), participant.Credential);
        var control = SignalCodec.Encode(new SignalMessage("relay-close", Guid.NewGuid()));
        var payload = new byte[control.Length + 1];
        control.CopyTo(payload, 1);
        using var sender = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await sender.SendAsync(RelayDatagramCodec.Encode(allocated.Credentials.RouteId, 1, 1,
            allocated.Credentials.Key, payload), IPEndPoint.Parse(allocated.Endpoint));
        var retryParticipant = invitations.Join(invitation.JoinCode, Guid.NewGuid());
        var attempt = Guid.NewGuid();
        RelayAllocation? replacement = null;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (replacement is null)
        {
            deadline.Token.ThrowIfCancellationRequested();
            await registry.CleanExpiredAsync();
            try
            {
                replacement = registry.Allocate(invitation.SessionId, retryParticipant.ParticipantId, attempt, retryParticipant.Credential);
            }
            catch (InvalidOperationException exception) when (exception.Message == "relay_capacity_reached")
            {
                await Task.Delay(5, deadline.Token);
            }
        }
        Assert.Equal(allocated.Endpoint, replacement.Endpoint);
        Assert.NotEqual(allocated.Credentials.RouteId, replacement.Credentials.RouteId);
        Assert.NotEqual(allocated.Credentials.Key, replacement.Credentials.Key);
    }

    [Fact]
    public async Task RenewalKeepsRouteIdentityAndIgnoresAcknowledgementOfAnOlderLease()
    {
        var clock = new ManualClock();
        var invitations = new InvitationRegistry(clock, TimeSpan.FromMinutes(5), 1);
        var invitation = invitations.Create(1);
        var participant = invitations.Join(invitation.JoinCode, Guid.NewGuid());
        await using var registry = new RelayRouteRegistry(invitations, clock, IPAddress.Loopback, IPAddress.Loopback, 0, 1, lifetimeSeconds: 120);
        var attempt = Guid.NewGuid();
        var first = registry.Allocate(invitation.SessionId, participant.ParticipantId, attempt, participant.Credential);
        Assert.Equal(clock.Now + TimeSpan.FromSeconds(120), first.ExpiresAt);
        registry.AcknowledgeHost(invitation.SessionId, participant.ParticipantId, attempt, invitation.HostCredential, first.ExpiresAt);
        clock.Now += TimeSpan.FromSeconds(10);
        Assert.Throws<InvalidOperationException>(() => registry.RenewPreparation(invitation.SessionId, participant.ParticipantId, attempt, "wrong"));
        var renewed = registry.RenewPreparation(invitation.SessionId, participant.ParticipantId, attempt, participant.Credential);
        Assert.Equal(first.Credentials.RouteId, renewed.Credentials.RouteId);
        Assert.Equal(first.Credentials.Key, renewed.Credentials.Key);
        Assert.Equal(first.Endpoint, renewed.Endpoint);
        Assert.True(renewed.ExpiresAt > first.ExpiresAt);
        registry.AcknowledgeHost(invitation.SessionId, participant.ParticipantId, attempt, invitation.HostCredential, first.ExpiresAt);
        var offer = Assert.Single(registry.GetHostOffers(invitation.SessionId, invitation.HostCredential));
        Assert.Equal(renewed.ExpiresAt, offer.ExpiresAt);
        registry.AcknowledgeHost(invitation.SessionId, participant.ParticipantId, attempt, invitation.HostCredential, renewed.ExpiresAt);
        Assert.Empty(registry.GetHostOffers(invitation.SessionId, invitation.HostCredential));
        clock.Now += TimeSpan.FromSeconds(121);
        Assert.Throws<InvalidOperationException>(() => registry.RenewPreparation(invitation.SessionId, participant.ParticipantId, attempt, participant.Credential));
    }

    [Fact]
    public async Task AllocationIsAuthorizedIdempotentAndSeparatesHostAndClientKeys()
    {
        var clock = new ManualClock();
        var invitations = new InvitationRegistry(clock, TimeSpan.FromMinutes(5), 1);
        var invitation = invitations.Create(2);
        var first = invitations.Join(invitation.JoinCode, Guid.NewGuid());
        var second = invitations.Join(invitation.JoinCode, Guid.NewGuid());
        await using var registry = new RelayRouteRegistry(invitations, clock, IPAddress.Loopback,
            IPAddress.Loopback, 0, 1);
        var attempt = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => registry.Allocate(invitation.SessionId, first.ParticipantId,
            attempt, second.Credential));
        var allocated = registry.Allocate(invitation.SessionId, first.ParticipantId, attempt, first.Credential);
        var retry = registry.Allocate(invitation.SessionId, first.ParticipantId, attempt, first.Credential);
        Assert.Equal(allocated.Endpoint, retry.Endpoint);
        Assert.Equal(allocated.Credentials.Key, retry.Credentials.Key);
        var host = Assert.Single(registry.GetHostOffers(invitation.SessionId, invitation.HostCredential));
        Assert.Equal(allocated.Credentials.RouteId, host.Credentials.RouteId);
        Assert.NotEqual(allocated.Credentials.Key, host.Credentials.Key);
        registry.AcknowledgeHost(invitation.SessionId, first.ParticipantId, attempt, invitation.HostCredential, host.ExpiresAt);
        Assert.Empty(registry.GetHostOffers(invitation.SessionId, invitation.HostCredential));
        Assert.Throws<InvalidOperationException>(() => registry.Allocate(invitation.SessionId, second.ParticipantId,
            Guid.NewGuid(), second.Credential));
        clock.Now += TimeSpan.FromMinutes(1);
        await registry.CleanExpiredAsync();
        Assert.NotEqual(allocated.Credentials.RouteId,
            registry.Allocate(invitation.SessionId, second.ParticipantId, Guid.NewGuid(), second.Credential).Credentials.RouteId);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task FailedRouteDoesNotInterruptOtherRoutesAndItsPortCanBeReused(bool socketFailure, bool loggingFailure)
    {
        var firstPort = ReserveAdjacentPorts();
        var logs = new CapturedLogs { ThrowRouteLogs = loggingFailure };
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var invitations = new InvitationRegistry(TimeProvider.System, TimeSpan.FromMinutes(5), 1);
        var invitation = invitations.Create(3);
        var failedParticipant = invitations.Join(invitation.JoinCode, Guid.NewGuid());
        var healthyParticipant = invitations.Join(invitation.JoinCode, Guid.NewGuid());
        await using var registry = new RelayRouteRegistry(invitations, TimeProvider.System, IPAddress.Loopback,
            IPAddress.Loopback, firstPort, 2, loggerFactory: loggerFactory);
        var failed = registry.Allocate(invitation.SessionId, failedParticipant.ParticipantId, Guid.NewGuid(), failedParticipant.Credential);
        var healthy = registry.Allocate(invitation.SessionId, healthyParticipant.ParticipantId, Guid.NewGuid(), healthyParticipant.Credential);
        var hostOffer = registry.GetHostOffers(invitation.SessionId, invitation.HostCredential)
            .Single(offer => offer.Credentials.RouteId == healthy.Credentials.RouteId);
        var failedRoute = FindRoute(registry, failed.Credentials.RouteId);
        var socket = (UdpClient)typeof(UdpRelayRoute).GetField("_socket", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(failedRoute)!;
        if (socketFailure)
        {
            IPEndPoint closedEndpoint;
            using (var reservation = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                closedEndpoint = (IPEndPoint)reservation.Client.LocalEndPoint!;
            }
            // A connected UDP socket receives the ICMP refusal from this closed local port.
            socket.Connect(closedEndpoint);
            await socket.SendAsync(new byte[] { 1 });
        }
        else
        {
            // Dispose without requesting route cancellation to simulate an unexpected socket failure.
            socket.Dispose();
        }
        if (loggingFailure)
        {
            await Assert.ThrowsAsync<AggregateException>(() => failedRoute.Completion.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        else
        {
            await failedRoute.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(failedRoute.Completion.IsCompletedSuccessfully);
        }
        Assert.Contains(logs.Items, item => item.Level >= LogLevel.Warning &&
            item.Message.Contains(failed.Credentials.RouteId.ToString(), StringComparison.Ordinal));
        await registry.CleanExpiredAsync();
        await registry.CleanExpiredAsync();
        if (loggingFailure)
        {
            Assert.Contains(logs.Items, item => item.Level == LogLevel.Error &&
                item.Message.Contains("Could not dispose relay route", StringComparison.Ordinal));
        }

        using var host = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var healthyEndpoint = IPEndPoint.Parse(healthy.Endpoint);
        await host.SendAsync(RelayDatagramCodec.Encode(hostOffer.Credentials.RouteId, 0, 1, hostOffer.Credentials.Key,
            "register-host"u8), healthyEndpoint);
        await client.SendAsync(RelayDatagramCodec.Encode(healthy.Credentials.RouteId, 1, 1, healthy.Credentials.Key,
            "register-client"u8), healthyEndpoint);
        await host.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        await host.SendAsync(RelayDatagramCodec.Encode(hostOffer.Credentials.RouteId, 0, 2, hostOffer.Credentials.Key,
            "healthy-route"u8), healthyEndpoint);
        var received = await client.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(RelayDatagramCodec.TryDecode(received.Buffer, healthy.Credentials.Key, out var packet));
        Assert.Equal("healthy-route"u8.ToArray(), packet!.Payload);

        var replacementParticipant = invitations.Join(invitation.JoinCode, Guid.NewGuid());
        var replacement = registry.Allocate(invitation.SessionId, replacementParticipant.ParticipantId, Guid.NewGuid(),
            replacementParticipant.Credential);
        Assert.Equal(failed.Endpoint, replacement.Endpoint);
        Assert.NotEqual(failed.Credentials.RouteId, replacement.Credentials.RouteId);
        Assert.NotEqual(failed.Credentials.Key, replacement.Credentials.Key);
    }

    [Fact]
    public async Task NormalRouteCancellationDoesNotLogFailure()
    {
        var logs = new CapturedLogs();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        await using var route = new UdpRelayRoute(new IPEndPoint(IPAddress.Loopback, 0),
            logger: loggerFactory.CreateLogger<UdpRelayRoute>());
        await route.DisposeAsync();
        Assert.Empty(logs.Items);
    }

    private static UdpRelayRoute FindRoute(RelayRouteRegistry registry, Guid id)
    {
        var routes = (IDictionary)typeof(RelayRouteRegistry).GetField("_routes", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(registry)!;
        return routes.Values.Cast<object>().Select(entry => (UdpRelayRoute)entry.GetType().GetProperty("Route")!.GetValue(entry)!)
            .Single(route => route.Id == id);
    }

    private static int ReserveAdjacentPorts()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            using var first = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            var port = ((IPEndPoint)first.Client.LocalEndPoint!).Port;
            if (port == ushort.MaxValue)
            {
                continue;
            }
            try
            {
                using var second = new UdpClient(new IPEndPoint(IPAddress.Loopback, port + 1));
                return port;
            }
            catch (SocketException)
            {
            }
        }
        throw new InvalidOperationException("Could not reserve adjacent UDP ports for the relay test.");
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        public ConcurrentQueue<(LogLevel Level, string Message)> Items { get; } = new();

        public bool ThrowRouteLogs { get; init; }

        public ILogger CreateLogger(string categoryName) => new CapturedLogger(this, categoryName);

        public void Dispose()
        {
        }

        private sealed class CapturedLogger(CapturedLogs logs, string categoryName) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                logs.Items.Enqueue((logLevel, formatter(state, exception)));
                if (logs.ThrowRouteLogs && categoryName == typeof(UdpRelayRoute).FullName)
                {
                    throw new InvalidOperationException("Injected route logging failure.");
                }
            }
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
