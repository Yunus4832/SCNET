using System.Net;
using System.Net.Sockets;

using RendezvousServer.Sessions;

namespace RendezvousServer.Test;

public sealed class EndpointRegistrationsTest
{
    [Fact]
    public void ExchangeRetriesShareChallengeAndRejectChangedEndpointsOrClosedSession()
    {
        var clock = new ManualClock();
        var registry = new InvitationRegistry(clock, TimeSpan.FromMinutes(5), 1);
        var invitation = registry.Create(1);
        var participant = registry.Join(invitation.JoinCode, Guid.NewGuid());
        var registrations = new EndpointRegistrations(registry, clock);
        var hostEndpoint = new IPEndPoint(IPAddress.Loopback, 30000);
        var participantEndpoint = new IPEndPoint(IPAddress.Loopback, 30001);
        var hostTicket = registry.IssueTicket(invitation.SessionId, Guid.Empty, invitation.HostCredential);
        var participantTicket = registry.IssueTicket(invitation.SessionId, participant.ParticipantId,
            participant.Credential);
        var hostChallenge = registrations.Challenge(invitation.SessionId, Guid.Empty, hostTicket.Token, hostEndpoint);
        var participantChallenge = registrations.Challenge(invitation.SessionId, participant.ParticipantId,
            participantTicket.Token, participantEndpoint);
        Assert.True(registrations.Confirm(invitation.SessionId, Guid.Empty, hostTicket.Token, hostChallenge, hostEndpoint));
        Assert.True(registrations.Confirm(invitation.SessionId, participant.ParticipantId, participantTicket.Token,
            participantChallenge, participantEndpoint));
        var attempt = Guid.NewGuid();
        var challenge = registrations.ExchangeChallenge(invitation.SessionId, participant.ParticipantId,
            attempt, participantEndpoint, hostEndpoint);
        Assert.Equal(challenge, registrations.ExchangeChallenge(invitation.SessionId, participant.ParticipantId,
            attempt, participantEndpoint, hostEndpoint));
        Assert.NotEqual(challenge, registrations.ExchangeChallenge(invitation.SessionId, participant.ParticipantId,
            Guid.NewGuid(), participantEndpoint, hostEndpoint));
        Assert.Throws<InvalidOperationException>(() => registrations.ExchangeChallenge(invitation.SessionId,
            participant.ParticipantId, attempt, hostEndpoint, hostEndpoint));
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.NotEqual(challenge, registrations.ExchangeChallenge(invitation.SessionId, participant.ParticipantId,
            attempt, participantEndpoint, hostEndpoint));
        registry.Close(invitation.SessionId, invitation.HostCredential);
        Assert.Throws<InvalidOperationException>(() => registrations.ExchangeChallenge(invitation.SessionId,
            participant.ParticipantId, attempt, participantEndpoint, hostEndpoint));
    }

    [Fact]
    public void ConfirmedLeaseSurvivesTicketExpiryButHasItsOwnDeadline()
    {
        var clock = new ManualClock();
        var registry = new InvitationRegistry(clock, TimeSpan.FromMinutes(5), 1);
        var invitation = registry.Create(1);
        var ticket = registry.IssueTicket(invitation.SessionId, Guid.Empty, invitation.HostCredential);
        var registrations = new EndpointRegistrations(registry, clock);
        var endpoint = new IPEndPoint(IPAddress.Loopback, 30000);
        var challenge = registrations.Challenge(invitation.SessionId, Guid.Empty, ticket.Token, endpoint);
        Assert.True(registrations.Confirm(invitation.SessionId, Guid.Empty, ticket.Token, challenge, endpoint));
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.False(registry.AuthenticateTicket(invitation.SessionId, Guid.Empty, ticket.Token));
        Assert.Equal(endpoint, registrations.Find(invitation.SessionId, Guid.Empty, AddressFamily.InterNetwork));
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Null(registrations.Find(invitation.SessionId, Guid.Empty, AddressFamily.InterNetwork));
    }

    [Fact]
    public void DuplicateConfirmationDoesNotExtendLease()
    {
        var clock = new ManualClock();
        var registry = new InvitationRegistry(clock, TimeSpan.FromMinutes(5), 1);
        var invitation = registry.Create(1);
        var ticket = registry.IssueTicket(invitation.SessionId, Guid.Empty, invitation.HostCredential);
        var registrations = new EndpointRegistrations(registry, clock);
        var endpoint = new IPEndPoint(IPAddress.Loopback, 30000);
        var challenge = registrations.Challenge(invitation.SessionId, Guid.Empty, ticket.Token, endpoint);
        Assert.True(registrations.Confirm(invitation.SessionId, Guid.Empty, ticket.Token, challenge, endpoint));
        clock.Advance(TimeSpan.FromSeconds(20));
        Assert.True(registrations.Confirm(invitation.SessionId, Guid.Empty, ticket.Token, challenge, endpoint));
        clock.Advance(TimeSpan.FromSeconds(40));
        Assert.Null(registrations.Find(invitation.SessionId, Guid.Empty, AddressFamily.InterNetwork));
    }

    [Fact]
    public void CandidateRequiresCorrectChallengeFromOriginalEndpoint()
    {
        var registry = new InvitationRegistry(TimeProvider.System, TimeSpan.FromMinutes(5), 1);
        var invitation = registry.Create(1);
        var ticket = registry.IssueTicket(invitation.SessionId, Guid.Empty, invitation.HostCredential);
        var registrations = new EndpointRegistrations(registry, TimeProvider.System);
        var endpoint = new IPEndPoint(IPAddress.Loopback, 30000);
        var other = new IPEndPoint(IPAddress.Loopback, 30001);
        var challenge = registrations.Challenge(invitation.SessionId, Guid.Empty, ticket.Token, endpoint);
        Assert.Null(registrations.Find(invitation.SessionId, Guid.Empty, AddressFamily.InterNetwork));
        Assert.False(registrations.Confirm(invitation.SessionId, Guid.Empty, ticket.Token, "wrong", endpoint));
        Assert.False(registrations.Confirm(invitation.SessionId, Guid.Empty, ticket.Token, challenge, other));
        Assert.Throws<InvalidOperationException>(() =>
            registrations.Challenge(invitation.SessionId, Guid.Empty, ticket.Token, other));
        Assert.Equal(challenge, registrations.Challenge(invitation.SessionId, Guid.Empty, ticket.Token, endpoint));
        Assert.True(registrations.Confirm(invitation.SessionId, Guid.Empty, ticket.Token, challenge, endpoint));
        Assert.Equal(endpoint, registrations.Find(invitation.SessionId, Guid.Empty, AddressFamily.InterNetwork));
        registry.Close(invitation.SessionId, invitation.HostCredential);
        Assert.Null(registrations.Find(invitation.SessionId, Guid.Empty, AddressFamily.InterNetwork));
    }
    [Fact]
    public void DualStackRegistrationsRetainBothFamiliesAndBindChallengesToTheirEndpoints()
    {
        var clock = new ManualClock();
        var registry = new InvitationRegistry(clock, TimeSpan.FromMinutes(5), 1);
        var invitation = registry.Create(1);
        var ticket = registry.IssueTicket(invitation.SessionId, Guid.Empty, invitation.HostCredential);
        var registrations = new EndpointRegistrations(registry, clock);
        var ipv4 = new IPEndPoint(IPAddress.Loopback, 30000);
        var ipv6 = new IPEndPoint(IPAddress.IPv6Loopback, 30000);
        var challenge4 = registrations.Challenge(invitation.SessionId, Guid.Empty, ticket.Token, ipv4);
        var challenge6 = registrations.Challenge(invitation.SessionId, Guid.Empty, ticket.Token, ipv6);
        Assert.NotEqual(challenge4, challenge6);
        Assert.False(registrations.Confirm(invitation.SessionId, Guid.Empty, ticket.Token, challenge4, ipv6));
        Assert.False(registrations.Confirm(invitation.SessionId, Guid.Empty, ticket.Token, challenge6, ipv4));
        Assert.True(registrations.Confirm(invitation.SessionId, Guid.Empty, ticket.Token, challenge4, ipv4));
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.True(registrations.Confirm(invitation.SessionId, Guid.Empty, ticket.Token, challenge6, ipv6));
        Assert.Equal(ipv4, registrations.Find(invitation.SessionId, Guid.Empty, AddressFamily.InterNetwork));
        Assert.Equal(ipv6, registrations.Find(invitation.SessionId, Guid.Empty, AddressFamily.InterNetworkV6));
        clock.Advance(TimeSpan.FromSeconds(55));
        Assert.Null(registrations.Find(invitation.SessionId, Guid.Empty, AddressFamily.InterNetwork));
        Assert.Equal(ipv6, registrations.Find(invitation.SessionId, Guid.Empty, AddressFamily.InterNetworkV6));
        registry.Close(invitation.SessionId, invitation.HostCredential);
        Assert.Null(registrations.Find(invitation.SessionId, Guid.Empty, AddressFamily.InterNetworkV6));
    }

    [Fact]
    public void LocalMetadataIsBoundToChallengeAndPublishedAsSnapshot()
    {
        var clock = new ManualClock();
        var registry = new InvitationRegistry(clock, TimeSpan.FromMinutes(5), 1);
        var invitation = registry.Create(1);
        var ticket = registry.IssueTicket(invitation.SessionId, Guid.Empty, invitation.HostCredential);
        var registrations = new EndpointRegistrations(registry, clock, lifetimeSeconds: 120);
        var endpoint = new IPEndPoint(IPAddress.Loopback, 30000);
        string[] local = ["[240e:1234::2]:30000"];
        var challenge = registrations.Challenge(invitation.SessionId, Guid.Empty, ticket.Token, endpoint, local);
        local[0] = "192.168.1.3:30000";
        Assert.Throws<InvalidOperationException>(() =>
            registrations.Challenge(invitation.SessionId, Guid.Empty, ticket.Token, endpoint, local));
        Assert.True(registrations.Confirm(invitation.SessionId, Guid.Empty, ticket.Token, challenge, endpoint));
        var published = registrations.FindLocalEndpoints(invitation.SessionId, Guid.Empty, AddressFamily.InterNetwork);
        Assert.Equal("[240e:1234::2]:30000", Assert.Single(published));
        published[0] = "192.168.1.4:30000";
        Assert.Equal("[240e:1234::2]:30000", Assert.Single(
            registrations.FindLocalEndpoints(invitation.SessionId, Guid.Empty, AddressFamily.InterNetwork)));
        clock.Advance(TimeSpan.FromSeconds(61));
        Assert.NotEmpty(registrations.FindLocalEndpoints(invitation.SessionId, Guid.Empty, AddressFamily.InterNetwork));
        clock.Advance(TimeSpan.FromSeconds(59));
        Assert.Empty(registrations.FindLocalEndpoints(invitation.SessionId, Guid.Empty, AddressFamily.InterNetwork));
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }
}
