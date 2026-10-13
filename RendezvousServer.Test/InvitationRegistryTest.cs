using RendezvousServer.Sessions;

namespace RendezvousServer.Test;

public sealed class InvitationRegistryTest
{
    [Fact]
    public void ParticipantRenewalUsesActiveHostLeaseAndDoesNotReviveExpiredRegistrations()
    {
        var clock = new ManualClock();
        var registry = new InvitationRegistry(clock, TimeSpan.FromMinutes(1), 1);
        var invitation = registry.Create(1);
        var request = Guid.NewGuid();
        var participant = registry.Join(invitation.JoinCode, request);
        clock.Advance(TimeSpan.FromSeconds(30));
        var until = registry.Renew(invitation.SessionId, invitation.HostCredential);
        Assert.Throws<InvalidOperationException>(() => registry.RenewParticipant(invitation.SessionId, participant.ParticipantId, "wrong"));
        Assert.Equal(until, registry.RenewParticipant(invitation.SessionId, participant.ParticipantId, participant.Credential));
        clock.Advance(TimeSpan.FromSeconds(35));
        Assert.True(registry.Authenticate(invitation.SessionId, participant.ParticipantId, participant.Credential));
        Assert.Equal(until, registry.JoinSession(invitation.SessionId, request).ExpiresAt);
        clock.Advance(TimeSpan.FromSeconds(26));
        Assert.Throws<InvalidOperationException>(() => registry.RenewParticipant(invitation.SessionId, participant.ParticipantId, participant.Credential));
    }

    [Fact]
    public void LeavingReleasesCapacityAndTicketsWithoutRevivingRetriedJoinRequests()
    {
        var registry = new InvitationRegistry(TimeProvider.System, TimeSpan.FromMinutes(5), 10);
        var invitation = registry.Create(1);
        var request = Guid.NewGuid();
        var participant = registry.Join(invitation.JoinCode, request);
        var ticket = registry.IssueTicket(invitation.SessionId, participant.ParticipantId, participant.Credential);
        Assert.Throws<InvalidOperationException>(() => registry.Leave(invitation.SessionId, participant.ParticipantId, "wrong"));
        Assert.True(registry.Authenticate(invitation.SessionId, participant.ParticipantId, participant.Credential));
        registry.Leave(invitation.SessionId, participant.ParticipantId, participant.Credential);
        registry.Leave(invitation.SessionId, participant.ParticipantId, participant.Credential);
        Assert.False(registry.Authenticate(invitation.SessionId, participant.ParticipantId, participant.Credential));
        Assert.False(registry.IsActive(invitation.SessionId, participant.ParticipantId));
        Assert.False(registry.AuthenticateTicket(invitation.SessionId, participant.ParticipantId, ticket.Token));
        var retried = Assert.Throws<InvalidOperationException>(() => registry.Join(invitation.JoinCode, request));
        Assert.Equal("join_request_expired", retried.Message);
        Assert.NotEqual(participant.ParticipantId, registry.JoinSession(invitation.SessionId, Guid.NewGuid()).ParticipantId);
        registry.Close(invitation.SessionId, invitation.HostCredential);
        registry.Leave(invitation.SessionId, participant.ParticipantId, participant.Credential);
    }

    [Fact]
    public void SessionLocatorSharesJoinIdempotencyAndCannotRestoreAClosedInvitation()
    {
        var registry = new InvitationRegistry(TimeProvider.System, TimeSpan.FromMinutes(5), 10);
        var invitation = registry.Create(2);
        var request = Guid.NewGuid();
        var first = registry.Join(invitation.JoinCode, request);
        Assert.Equal(first, registry.JoinSession(invitation.SessionId, request));
        var restored = registry.JoinSession(invitation.SessionId, Guid.NewGuid());
        Assert.NotEqual(first.ParticipantId, restored.ParticipantId);
        Assert.Throws<InvalidOperationException>(() => registry.JoinSession(invitation.SessionId, Guid.NewGuid()));
        registry.Close(invitation.SessionId, invitation.HostCredential);
        var failure = Assert.Throws<InvalidOperationException>(() => registry.JoinSession(invitation.SessionId, Guid.NewGuid()));
        Assert.Equal("invitation_not_found", failure.Message);
    }

    [Fact]
    public void ConcurrentJoinRetriesAllocateOnlyOneParticipant()
    {
        var registry = new InvitationRegistry(TimeProvider.System, TimeSpan.FromMinutes(5), 2);
        var invitation = registry.Create(1);
        var requestId = Guid.NewGuid();
        var registrations = new System.Collections.Concurrent.ConcurrentBag<Guid>();
        Parallel.For(0, 32, _ => registrations.Add(registry.Join(invitation.JoinCode, requestId).ParticipantId));
        Assert.Single(registrations.Distinct());
        Assert.Throws<InvalidOperationException>(() => registry.Join(invitation.JoinCode, Guid.NewGuid()));
        var other = registry.Create(1);
        Assert.NotEqual(registrations.First(), registry.Join(other.JoinCode, requestId).ParticipantId);
    }

    [Fact]
    public void ExpiredJoinRequestCannotAllocateReplacementParticipant()
    {
        var clock = new ManualClock();
        var registry = new InvitationRegistry(clock, TimeSpan.FromMinutes(5), 1);
        var invitation = registry.Create(1);
        var requestId = Guid.NewGuid();
        registry.Join(invitation.JoinCode, requestId);
        clock.Advance(TimeSpan.FromMinutes(4));
        registry.Renew(invitation.SessionId, invitation.HostCredential);
        clock.Advance(TimeSpan.FromMinutes(1));
        var error = Assert.Throws<InvalidOperationException>(() => registry.Join(invitation.JoinCode, requestId));
        Assert.Equal("join_request_expired", error.Message);
        Assert.Throws<InvalidOperationException>(() => registry.Join(invitation.JoinCode, Guid.Empty));
        Assert.NotEqual(Guid.Empty, registry.Join(invitation.JoinCode, Guid.NewGuid()).ParticipantId);
    }

    [Fact]
    public void CredentialsCannotAuthorizeAnotherSessionOrParticipant()
    {
        var registry = new InvitationRegistry(TimeProvider.System, TimeSpan.FromMinutes(5), 10);
        var first = registry.Create(2);
        var second = registry.Create(2);
        var participant = registry.Join(first.JoinCode, Guid.NewGuid());
        Assert.True(registry.Authenticate(first.SessionId, participant.ParticipantId, participant.Credential));
        Assert.False(registry.Authenticate(second.SessionId, participant.ParticipantId, participant.Credential));
        Assert.False(registry.Authenticate(first.SessionId, Guid.Empty, participant.Credential));
        Assert.Throws<InvalidOperationException>(() => registry.Close(first.SessionId, second.HostCredential));
        Assert.True(registry.Authenticate(first.SessionId, Guid.Empty, first.HostCredential));
    }

    [Fact]
    public void ExpiredParticipantReleasesCapacityWhileRenewedInvitationRemainsOpen()
    {
        var clock = new ManualClock();
        var registry = new InvitationRegistry(clock, TimeSpan.FromMinutes(5), 1);
        var invitation = registry.Create(1);
        var participant = registry.Join(invitation.JoinCode, Guid.NewGuid());
        clock.Advance(TimeSpan.FromMinutes(4));
        registry.Renew(invitation.SessionId, invitation.HostCredential);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.False(registry.Authenticate(invitation.SessionId, participant.ParticipantId, participant.Credential));
        var replacement = registry.Join(invitation.JoinCode, Guid.NewGuid());
        Assert.True(registry.Authenticate(invitation.SessionId, replacement.ParticipantId, replacement.Credential));
    }

    [Fact]
    public void ExpiredAndClosedInvitationsInvalidateCodesAndCredentials()
    {
        var clock = new ManualClock();
        var registry = new InvitationRegistry(clock, TimeSpan.FromMinutes(5), 1);
        var invitation = registry.Create(1);
        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Throws<InvalidOperationException>(() => registry.Join(invitation.JoinCode, Guid.NewGuid()));
        Assert.False(registry.Authenticate(invitation.SessionId, Guid.Empty, invitation.HostCredential));
        var replacement = registry.Create(1);
        registry.Close(replacement.SessionId, replacement.HostCredential);
        Assert.Throws<InvalidOperationException>(() => registry.Join(replacement.JoinCode, Guid.NewGuid()));
        Assert.False(registry.Authenticate(replacement.SessionId, Guid.Empty, replacement.HostCredential));
    }

    [Fact]
    public void ConcurrentJoinCannotExceedCapacity()
    {
        var registry = new InvitationRegistry(TimeProvider.System, TimeSpan.FromMinutes(5), 1);
        var invitation = registry.Create(4);
        var accepted = 0;
        Parallel.For(0, 32, _ =>
        {
            try
            {
                registry.Join(invitation.JoinCode, Guid.NewGuid());
                Interlocked.Increment(ref accepted);
            }
            catch (InvalidOperationException)
            {
            }
        });
        Assert.Equal(4, accepted);
    }

    [Fact]
    public void RegistrationTicketExpiresAndCannotCrossParticipants()
    {
        var clock = new ManualClock();
        var registry = new InvitationRegistry(clock, TimeSpan.FromMinutes(5), 2);
        var invitation = registry.Create(2);
        var participant = registry.Join(invitation.JoinCode, Guid.NewGuid());
        var ticket = registry.IssueTicket(invitation.SessionId, participant.ParticipantId, participant.Credential);
        Assert.True(registry.AuthenticateTicket(invitation.SessionId, participant.ParticipantId, ticket.Token));
        Assert.False(registry.AuthenticateTicket(invitation.SessionId, Guid.Empty, ticket.Token));
        Assert.False(registry.AuthenticateTicket(invitation.SessionId, participant.ParticipantId, participant.Credential));
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.False(registry.AuthenticateTicket(invitation.SessionId, participant.ParticipantId, ticket.Token));
        Assert.True(registry.Authenticate(invitation.SessionId, participant.ParticipantId, participant.Credential));
    }

    [Fact]
    public void RenewingTicketRevokesOldCapabilityAndClosingSessionRevokesNewOne()
    {
        var registry = new InvitationRegistry(TimeProvider.System, TimeSpan.FromMinutes(5), 1);
        var invitation = registry.Create(1);
        var first = registry.IssueTicket(invitation.SessionId, Guid.Empty, invitation.HostCredential);
        var second = registry.IssueTicket(invitation.SessionId, Guid.Empty, invitation.HostCredential);
        Assert.False(registry.AuthenticateTicket(invitation.SessionId, Guid.Empty, first.Token));
        Assert.True(registry.AuthenticateTicket(invitation.SessionId, Guid.Empty, second.Token));
        registry.Close(invitation.SessionId, invitation.HostCredential);
        Assert.False(registry.AuthenticateTicket(invitation.SessionId, Guid.Empty, second.Token));
    }

    [Fact]
    public void ConfiguredTicketLifetimeControlsExpiry()
    {
        var clock = new ManualClock();
        var registry = new InvitationRegistry(clock, TimeSpan.FromMinutes(5), 1, ticketLifetimeSeconds: 5);
        var invitation = registry.Create(1);
        var ticket = registry.IssueTicket(invitation.SessionId, Guid.Empty, invitation.HostCredential);
        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.True(registry.AuthenticateTicket(invitation.SessionId, Guid.Empty, ticket.Token));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(registry.AuthenticateTicket(invitation.SessionId, Guid.Empty, ticket.Token));
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }
}
