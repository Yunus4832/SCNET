using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

using Rendezvous.Protocol;

namespace RendezvousServer.Sessions;

/// <summary>Verifies return reachability before publishing a transport endpoint.</summary>
public sealed class EndpointRegistrations(InvitationRegistry registry, TimeProvider clock, int lifetimeSeconds = 60)
{
    private readonly Dictionary<(string Ticket, AddressFamily Family), Registration> _registrations = new();

    private readonly Dictionary<(Guid Session, Guid Participant, AddressFamily Family), Lease> _leases = new();

    private readonly Dictionary<(Guid Session, Guid Participant, Guid Attempt), CandidateExchange> _exchanges = new();

    public string ExchangeChallenge(
        Guid sessionId,
        Guid participantId,
        Guid attemptId,
        IPEndPoint participantEndpoint,
        IPEndPoint hostEndpoint
    )
    {
        CleanExpired();
        if (participantId == Guid.Empty ||
            attemptId == Guid.Empty ||
            participantEndpoint.AddressFamily != hostEndpoint.AddressFamily ||
            !Equals(Find(sessionId, participantId, participantEndpoint.AddressFamily), participantEndpoint) ||
            !Equals(Find(sessionId, Guid.Empty, hostEndpoint.AddressFamily), hostEndpoint))
        {
            throw new InvalidOperationException("invitation_unauthorized");
        }

        var key = (sessionId, participantId, attemptId);
        if (_exchanges.TryGetValue(key, out var existing))
        {
            if (!existing.ParticipantEndpoint.Equals(participantEndpoint) ||
                !existing.HostEndpoint.Equals(hostEndpoint))
            {
                throw new InvalidOperationException("candidate_endpoint_changed");
            }

            return existing.Challenge;
        }

        if (_exchanges.Count >= 4096)
        {
            throw new InvalidOperationException("candidate_capacity_exceeded");
        }

        var challenge = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _exchanges.Add(
            key,
            new CandidateExchange(
                participantEndpoint,
                hostEndpoint,
                challenge,
                clock.GetUtcNow() + TimeSpan.FromSeconds(10)
            )
        );
        return challenge;
    }

    public string Challenge(
        Guid sessionId,
        Guid participantId,
        string ticket,
        IPEndPoint endpoint,
        IReadOnlyList<string>? localEndpoints = null
    )
    {
        CleanExpired();
        if (!registry.AuthenticateTicket(sessionId, participantId, ticket))
        {
            throw new InvalidOperationException("registration_unauthorized");
        }

        if (!LocalCandidateEndpoints.TryParse(localEndpoints, null, out var candidates))
        {
            throw new InvalidOperationException("invalid_local_candidates");
        }

        if (_registrations.TryGetValue((ticket, endpoint.AddressFamily), out var existing))
        {
            if (!existing.Endpoint.Equals(endpoint) || !existing.LocalEndpoints.SequenceEqual(candidates))
            {
                throw new InvalidOperationException("registration_endpoint_mismatch");
            }

            return existing.Challenge;
        }

        var challenge = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        _registrations.Add(
            (ticket, endpoint.AddressFamily),
            new Registration(
                sessionId,
                participantId,
                endpoint,
                candidates,
                challenge,
                clock.GetUtcNow() + TimeSpan.FromSeconds(10)
            )
        );
        return challenge;
    }

    public bool Confirm(Guid sessionId, Guid participantId, string ticket, string? challenge, IPEndPoint endpoint)
    {
        CleanExpired();
        if (!registry.AuthenticateTicket(sessionId, participantId, ticket) ||
            !_registrations.TryGetValue((ticket, endpoint.AddressFamily), out var registration) ||
            !registration.Endpoint.Equals(endpoint) ||
            registration.Challenge != challenge)
        {
            return false;
        }

        if (!registration.Confirmed)
        {
            registration.Confirmed = true;
            _leases[(sessionId, participantId, endpoint.AddressFamily)] = new Lease(
                endpoint,
                registration.LocalEndpoints,
                clock.GetUtcNow() + TimeSpan.FromSeconds(lifetimeSeconds)
            );
            if (participantId == Guid.Empty)
            {
                registry.ConfirmHostEndpoint(sessionId, clock.GetUtcNow() + TimeSpan.FromSeconds(lifetimeSeconds));
            }
        }

        return true;
    }

    public IPEndPoint? Find(Guid sessionId, Guid participantId, AddressFamily family)
    {
        CleanExpired();
        return _leases.TryGetValue((sessionId, participantId, family), out var lease) ? lease.Endpoint : null;
    }

    public string[] FindLocalEndpoints(Guid sessionId, Guid participantId, AddressFamily family)
    {
        CleanExpired();
        return _leases.TryGetValue((sessionId, participantId, family), out var lease)
            ? lease.LocalEndpoints.Select(endpoint => endpoint.ToString()).ToArray()
            : [];
    }

    public void CleanExpired()
    {
        foreach (var key in _exchanges.Where(item => item.Value.ExpiresAt <= clock.GetUtcNow() ||
                                                     !registry.IsActive(item.Key.Session, item.Key.Participant)
                     )
                     .Select(item => item.Key).ToArray())
        {
            _exchanges.Remove(key);
        }

        foreach (var key in _leases.Where(item => item.Value.ExpiresAt <= clock.GetUtcNow() ||
                                                  !registry.IsActive(item.Key.Session, item.Key.Participant)
                     )
                     .Select(item => item.Key).ToArray())
        {
            _leases.Remove(key);
        }

        foreach (var key in _registrations.Where(item =>
                         !registry.AuthenticateTicket(
                             item.Value.SessionId,
                             item.Value.ParticipantId,
                             item.Key.Ticket
                         ) ||
                         (!item.Value.Confirmed && item.Value.Deadline <= clock.GetUtcNow())
                     )
                     .Select(item => item.Key).ToArray())
        {
            _registrations.Remove(key);
        }
    }

    private sealed record Lease(IPEndPoint Endpoint, IPEndPoint[] LocalEndpoints, DateTimeOffset ExpiresAt);

    private sealed record CandidateExchange(
        IPEndPoint ParticipantEndpoint,
        IPEndPoint HostEndpoint,
        string Challenge,
        DateTimeOffset ExpiresAt
    );

    private sealed class Registration(
        Guid sessionId,
        Guid participantId,
        IPEndPoint endpoint,
        IPEndPoint[] localEndpoints,
        string challenge,
        DateTimeOffset deadline
    )
    {
        public Guid SessionId { get; } = sessionId;

        public Guid ParticipantId { get; } = participantId;

        public IPEndPoint Endpoint { get; } = endpoint;

        public IPEndPoint[] LocalEndpoints { get; } = localEndpoints;

        public string Challenge { get; } = challenge;

        public DateTimeOffset Deadline { get; } = deadline;

        public bool Confirmed { get; set; }
    }
}
