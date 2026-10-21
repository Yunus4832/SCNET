using System.Globalization;
using System.Security.Cryptography;

using Rendezvous.Protocol;

namespace RendezvousServer.Sessions;

/// <summary>Owns short-lived invitations; all operations are atomic within this process.</summary>
public sealed class InvitationRegistry(
    TimeProvider clock,
    TimeSpan lifetime,
    int capacity,
    int ticketLifetimeSeconds = 30
)
{
    private readonly Lock _gate = new();

    private readonly Guid _directoryGeneration = Guid.NewGuid();

    private long _nextSequence;

    private readonly Dictionary<Guid, Invitation> _invitations = [];

    private readonly Dictionary<string, Guid> _codes = new(StringComparer.Ordinal);

    private readonly Dictionary<string, Ticket> _tickets = new(StringComparer.Ordinal);

    public SessionRegistration Create(int maxParticipants)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxParticipants, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxParticipants, 32);
        lock (_gate)
        {
            RemoveExpired();
            if (_invitations.Count >= capacity)
            {
                throw new InvalidOperationException("invitation_capacity_reached");
            }

            string code;
            do
            {
                code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
            } while (_codes.ContainsKey(code));

            var registration = new SessionRegistration(
                Guid.NewGuid(),
                code,
                NewCredential(),
                clock.GetUtcNow() + lifetime
            );
            _invitations.Add(
                registration.SessionId,
                new Invitation(registration, maxParticipants) { Sequence = ++_nextSequence }
            );
            _codes.Add(code, registration.SessionId);
            return registration;
        }
    }

    public void UpdateRoom(Guid sessionId, string credential, RoomPublication publication)
    {
        ArgumentNullException.ThrowIfNull(publication);
        if (!publication.IsValid)
        {
            throw new ArgumentException("invalid_room_publication", nameof(publication));
        }

        lock (_gate)
        {
            GetAuthorizedHost(sessionId, credential).Publication = publication with { Name = publication.Name.Trim() };
        }
    }

    internal void ConfirmHostEndpoint(Guid sessionId, DateTimeOffset expiresAt)
    {
        lock (_gate)
        {
            RemoveExpired();
            if (_invitations.TryGetValue(sessionId, out var invitation) && expiresAt > invitation.EndpointExpiresAt)
            {
                invitation.EndpointExpiresAt = expiresAt;
            }
        }
    }

    public RoomPage ListRooms(int limit, string? cursor)
    {
        if (limit is < 1 or > RendezvousLimits.MaximumRoomPageSize)
        {
            throw new ArgumentException("invalid_room_limit", nameof(limit));
        }

        lock (_gate)
        {
            RemoveExpired();
            var last = 0L;
            var upper = _nextSequence;
            if (cursor is not null)
            {
                var parts = cursor.Split(':');
                if (cursor.Length > RendezvousLimits.MaximumRoomCursorLength ||
                    parts.Length != 3 ||
                    !Guid.TryParseExact(parts[0], "N", out var generation) ||
                    generation != _directoryGeneration ||
                    !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out last) ||
                    !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out upper) ||
                    last < 0 ||
                    upper < last ||
                    upper > _nextSequence)
                {
                    throw new ArgumentException("invalid_room_cursor", nameof(cursor));
                }
            }

            var visible = _invitations.Values.Where(item => item.Sequence > last &&
                                                            item.Sequence <= upper &&
                                                            item.Publication?.IsPublic == true &&
                                                            item.EndpointExpiresAt > clock.GetUtcNow()
                )
                .OrderBy(item => item.Sequence).Take(limit + 1).ToArray();
            var page = visible.Take(limit).ToArray();
            var next = visible.Length > limit
                ? $"{_directoryGeneration:N}:{page[^1].Sequence.ToString(CultureInfo.InvariantCulture)}:{upper.ToString(CultureInfo.InvariantCulture)}"
                : null;
            return new RoomPage(
                page.Select(item => new RoomSummary(
                        item.Registration.SessionId,
                        item.Publication!.Name,
                        item.Publication.Description,
                        item.Registration.JoinCode,
                        ["holePunch", "relay"]
                    )
                ).ToArray(),
                next
            );
        }
    }

    public ParticipantRegistration Join(string code, Guid requestId)
    {
        if (requestId == Guid.Empty)
        {
            throw new InvalidOperationException("invalid_request_id");
        }

        if (string.IsNullOrEmpty(code) || code.Length != 6 || code.Any(character => character is < '0' or > '9'))
        {
            throw new InvalidOperationException("invalid_join_code");
        }

        lock (_gate)
        {
            RemoveExpired();
            if (!_codes.TryGetValue(code, out var sessionId))
            {
                throw new InvalidOperationException("invitation_not_found");
            }

            return JoinSessionLocked(sessionId, requestId);
        }
    }

    public ParticipantRegistration JoinSession(Guid sessionId, Guid requestId)
    {
        if (sessionId == Guid.Empty || requestId == Guid.Empty)
        {
            throw new InvalidOperationException("invalid_session_request");
        }

        lock (_gate)
        {
            RemoveExpired();
            return JoinSessionLocked(sessionId, requestId);
        }
    }

    private ParticipantRegistration JoinSessionLocked(Guid sessionId, Guid requestId)
    {
        if (!_invitations.TryGetValue(sessionId, out var invitation))
        {
            throw new InvalidOperationException("invitation_not_found");
        }

        if (invitation.JoinRequests.TryGetValue(requestId, out var previous))
        {
            if (!invitation.Participants.TryGetValue(previous.ParticipantId, out var current) ||
                current.ExpiresAt <= clock.GetUtcNow())
            {
                throw new InvalidOperationException("join_request_expired");
            }

            return current;
        }

        if (invitation.JoinRequests.Count >= 1024)
        {
            throw new InvalidOperationException("join_request_capacity_reached");
        }

        if (invitation.Participants.Count >= invitation.MaxParticipants)
        {
            throw new InvalidOperationException("invitation_full");
        }

        var participant = new ParticipantRegistration(
            sessionId,
            Guid.NewGuid(),
            NewCredential(),
            invitation.Registration.ExpiresAt
        );
        invitation.Participants.Add(participant.ParticipantId, participant);
        invitation.JoinRequests.Add(requestId, participant);
        return participant;
    }

    public void Leave(Guid sessionId, Guid participantId, string credential)
    {
        lock (_gate)
        {
            RemoveExpired();
            if (participantId == Guid.Empty)
            {
                throw new InvalidOperationException("unauthorized_participant");
            }

            if (!_invitations.TryGetValue(sessionId, out var invitation))
            {
                return;
            }

            var registration =
                invitation.JoinRequests.Values.FirstOrDefault(item => item.ParticipantId == participantId);
            if (registration is null || !Matches(registration.Credential, credential))
            {
                throw new InvalidOperationException("unauthorized_participant");
            }

            invitation.Participants.Remove(participantId);
            foreach (var token in _tickets.Where(item => item.Value.SessionId == sessionId &&
                                                         item.Value.ParticipantId == participantId
                         )
                         .Select(item => item.Key).ToArray())
            {
                _tickets.Remove(token);
            }
        }
    }

    public DateTimeOffset RenewParticipant(Guid sessionId, Guid participantId, string credential)
    {
        lock (_gate)
        {
            if (participantId == Guid.Empty || !Authenticate(sessionId, participantId, credential))
            {
                throw new InvalidOperationException("unauthorized_participant");
            }

            var invitation = _invitations[sessionId];
            var expiresAt = invitation.Registration.ExpiresAt;
            invitation.Participants[participantId] =
                invitation.Participants[participantId] with { ExpiresAt = expiresAt };
            return expiresAt;
        }
    }

    public DateTimeOffset Renew(Guid sessionId, string credential)
    {
        lock (_gate)
        {
            var invitation = GetAuthorizedHost(sessionId, credential);
            var expiresAt = clock.GetUtcNow() + lifetime;
            invitation.Registration = invitation.Registration with { ExpiresAt = expiresAt };
            return expiresAt;
        }
    }

    public void Close(Guid sessionId, string credential)
    {
        lock (_gate)
        {
            var invitation = GetAuthorizedHost(sessionId, credential);
            _codes.Remove(invitation.Registration.JoinCode);
            _invitations.Remove(sessionId);
        }
    }

    public bool Authenticate(Guid sessionId, Guid participantId, string credential)
    {
        lock (_gate)
        {
            RemoveExpired();
            if (!_invitations.TryGetValue(sessionId, out var invitation))
            {
                return false;
            }

            if (participantId == Guid.Empty)
            {
                return Matches(invitation.Registration.HostCredential, credential);
            }

            return invitation.Participants.TryGetValue(participantId, out var participant) &&
                   participant.ExpiresAt > clock.GetUtcNow() &&
                   Matches(participant.Credential, credential);
        }
    }

    public RegistrationTicket IssueTicket(Guid sessionId, Guid participantId, string credential)
    {
        lock (_gate)
        {
            if (!Authenticate(sessionId, participantId, credential))
            {
                throw new InvalidOperationException("invitation_unauthorized");
            }

            var invitation = _invitations[sessionId];
            var deadline = clock.GetUtcNow() + TimeSpan.FromSeconds(ticketLifetimeSeconds);
            var ownerDeadline = participantId == Guid.Empty
                ? invitation.Registration.ExpiresAt
                : invitation.Participants[participantId].ExpiresAt;
            var expiresAt = deadline < ownerDeadline ? deadline : ownerDeadline;
            // One outstanding registration capability per participant bounds storage.
            foreach (var token in _tickets.Where(item => item.Value.SessionId == sessionId &&
                                                         item.Value.ParticipantId == participantId
                         )
                         .Select(item => item.Key).ToArray())
            {
                _tickets.Remove(token);
            }

            var ticket = new RegistrationTicket(NewCredential(), expiresAt);
            _tickets.Add(ticket.Token, new Ticket(sessionId, participantId, expiresAt));
            return ticket;
        }
    }

    public bool AuthenticateTicket(Guid sessionId, Guid participantId, string token)
    {
        lock (_gate)
        {
            RemoveExpired();
            return !string.IsNullOrEmpty(token) &&
                   _tickets.TryGetValue(token, out var ticket) &&
                   ticket.SessionId == sessionId &&
                   ticket.ParticipantId == participantId &&
                   ticket.ExpiresAt > clock.GetUtcNow() &&
                   _invitations.TryGetValue(sessionId, out var invitation) &&
                   (participantId == Guid.Empty || invitation.Participants.ContainsKey(participantId));
        }
    }

    public bool IsActive(Guid sessionId, Guid participantId)
    {
        lock (_gate)
        {
            RemoveExpired();
            return _invitations.TryGetValue(sessionId, out var invitation) &&
                   (participantId == Guid.Empty || invitation.Participants.ContainsKey(participantId));
        }
    }

    private Invitation GetAuthorizedHost(Guid sessionId, string credential)
    {
        RemoveExpired();
        if (!_invitations.TryGetValue(sessionId, out var invitation) ||
            !Matches(invitation.Registration.HostCredential, credential))
        {
            throw new InvalidOperationException("invitation_unauthorized");
        }

        return invitation;
    }

    private void RemoveExpired()
    {
        var now = clock.GetUtcNow();
        foreach (var invitation in _invitations.Values.Where(item => item.Registration.ExpiresAt <= now).ToArray())
        {
            _codes.Remove(invitation.Registration.JoinCode);
            _invitations.Remove(invitation.Registration.SessionId);
        }

        foreach (var token in _tickets.Where(item => item.Value.ExpiresAt <= now ||
                                                     !_invitations.ContainsKey(item.Value.SessionId)
                     )
                     .Select(item => item.Key).ToArray())
        {
            _tickets.Remove(token);
        }

        foreach (var invitation in _invitations.Values)
        {
            foreach (var participant in invitation.Participants.Values.Where(item => item.ExpiresAt <= now).ToArray())
            {
                invitation.Participants.Remove(participant.ParticipantId);
            }
        }
    }

    private static string NewCredential() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static bool Matches(string expected, string supplied)
    {
        if (string.IsNullOrEmpty(supplied) || supplied.Length != expected.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(expected),
            System.Text.Encoding.ASCII.GetBytes(supplied)
        );
    }

    private sealed record Ticket(Guid SessionId, Guid ParticipantId, DateTimeOffset ExpiresAt);

    private sealed class Invitation(SessionRegistration registration, int maxParticipants)
    {
        public SessionRegistration Registration { get; set; } = registration;

        public long Sequence { get; init; }

        public RoomPublication? Publication { get; set; }

        public DateTimeOffset EndpointExpiresAt { get; set; }

        public int MaxParticipants { get; } = maxParticipants;

        public Dictionary<Guid, ParticipantRegistration> Participants { get; } = [];

        public Dictionary<Guid, ParticipantRegistration> JoinRequests { get; } = [];
    }
}
