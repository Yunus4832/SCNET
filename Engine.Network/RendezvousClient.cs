using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Rendezvous.Protocol;

namespace Engine.Network;

/// <summary>Exchanges invitation credentials over the configured control endpoint.</summary>
public sealed class RendezvousClient
{
    private readonly HttpClient _http;
    private readonly Uri _address;

    public RendezvousClient(HttpClient http, Uri address)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
        _address = RendezvousAddress.Normalize(address);
    }

    public async Task<RendezvousDescriptor> GetDescriptorAsync(CancellationToken cancellationToken)
    {
        return await RequestAsync<RendezvousDescriptor>("transport", null, cancellationToken).ConfigureAwait(false) ??
               throw InvalidResponse("transport");
    }

    public async Task<RoomPage> GetRoomsAsync(string? cursor, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, RendezvousLimits.MaximumRoomPageSize);
        if (cursor is { Length: > RendezvousLimits.MaximumRoomCursorLength })
        {
            throw new ArgumentException("Room cursor is too long.", nameof(cursor));
        }
        var path = $"rooms?limit={limit}" + (cursor is null ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}");
        var page = await RequestAsync<RoomPage>(path, null, cancellationToken).ConfigureAwait(false) ?? throw InvalidResponse(path);
        if (page.Rooms is null || page.Rooms.Count > limit || page.NextCursor is { Length: > RendezvousLimits.MaximumRoomCursorLength } ||
            page.Rooms.Any(room => room is null || room.Id == Guid.Empty || string.IsNullOrWhiteSpace(room.Name) ||
                room.Name.Length > RendezvousLimits.MaximumRoomNameLength || room.Description is null || room.Description.Length > RendezvousLimits.MaximumRoomDescriptionLength ||
                room.ConnectionMethods is null || room.ConnectionMethods.Count is < 1 or > 2 ||
                room.ConnectionMethods.Any(method => method is not ("holePunch" or "relay")) ||
                room.ConnectionMethods.Distinct().Count() != room.ConnectionMethods.Count ||
                room.JoinCode is null || room.JoinCode.Length != 6 || room.JoinCode.Any(character => character is < '0' or > '9')) ||
            page.Rooms.Select(room => room.Id).Distinct().Count() != page.Rooms.Count)
        {
            throw InvalidResponse(path);
        }
        return page;
    }

    public async Task UpdateRoomAsync(Guid sessionId, string credential, RoomPublication publication,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publication);
        if (!publication.IsValid)
        {
            throw new ArgumentException("Invalid room publication.", nameof(publication));
        }
        await RequestAsync<object>("invitations/update-room", new { SessionId = sessionId, Credential = credential, Publication = publication },
            cancellationToken).ConfigureAwait(false);
    }

    public Task<ParticipantRegistration> JoinSessionAsync(Guid sessionId, Guid requestId, CancellationToken cancellationToken) =>
        PostAsync<ParticipantRegistration>("invitations/join-session", new { SessionId = sessionId, RequestId = requestId }, cancellationToken);

    public Task<SessionRegistration> CreateAsync(int maxParticipants, CancellationToken cancellationToken) =>
        PostAsync<SessionRegistration>("invitations/create", new { MaxParticipants = maxParticipants }, cancellationToken);

    public Task<ParticipantRegistration> JoinAsync(string joinCode, Guid requestId, CancellationToken cancellationToken) =>
        PostAsync<ParticipantRegistration>("invitations/join", new { JoinCode = joinCode, RequestId = requestId }, cancellationToken);

    public Task<RegistrationTicket> IssueTicketAsync(Guid sessionId, Guid participantId, string credential,
        CancellationToken cancellationToken) => PostAsync<RegistrationTicket>("invitations/registration-ticket",
        new { SessionId = sessionId, ParticipantId = participantId, Credential = credential }, cancellationToken);

    public Task<InvitationRenewal> RenewAsync(Guid sessionId, string credential, CancellationToken cancellationToken) =>
        PostAsync<InvitationRenewal>("invitations/renew", new { SessionId = sessionId, Credential = credential }, cancellationToken);

    public Task<InvitationRenewal> RenewParticipantAsync(ParticipantRegistration participant, CancellationToken cancellationToken) =>
        PostAsync<InvitationRenewal>("invitations/renew-participant",
            new { participant.SessionId, participant.ParticipantId, participant.Credential }, cancellationToken);

    public Task<RelayAllocation> RenewRelayPreparationAsync(ParticipantRegistration participant, Guid attemptId,
        CancellationToken cancellationToken) => PostAsync<RelayAllocation>("relay/renew-preparation",
        new { participant.SessionId, participant.ParticipantId, AttemptId = attemptId, participant.Credential }, cancellationToken);

    public Task<RelayAllocation> AllocateRelayAsync(Guid sessionId, Guid participantId, Guid attemptId,
        string credential, CancellationToken cancellationToken) => PostAsync<RelayAllocation>("relay/allocate",
        new { SessionId = sessionId, ParticipantId = participantId, AttemptId = attemptId, Credential = credential }, cancellationToken);

    public Task<RelayAllocation[]> GetRelayOffersAsync(Guid sessionId, string credential, CancellationToken cancellationToken) =>
        PostAsync<RelayAllocation[]>("relay/host-offers", new { SessionId = sessionId, Credential = credential }, cancellationToken);

    public async Task AcknowledgeRelayAsync(RelayAllocation allocation, string credential, CancellationToken cancellationToken)
    {
        await RequestAsync<object>("relay/host-ready",
            new { allocation.SessionId, allocation.ParticipantId, allocation.AttemptId, allocation.ExpiresAt, Credential = credential },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task LeaveAsync(ParticipantRegistration participant, CancellationToken cancellationToken)
    {
        await RequestAsync<object>("invitations/leave",
            new { participant.SessionId, participant.ParticipantId, participant.Credential }, cancellationToken).ConfigureAwait(false);
    }

    public async Task CloseAsync(Guid sessionId, string credential, CancellationToken cancellationToken)
    {
        await RequestAsync<object>("invitations/close",
            new { SessionId = sessionId, Credential = credential }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> PostAsync<T>(string path, object request, CancellationToken cancellationToken)
    {
        return await RequestAsync<T>(path, request, cancellationToken).ConfigureAwait(false) ?? throw InvalidResponse(path);
    }

    private async Task<T?> RequestAsync<T>(string path, object? request, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_http.Timeout != Timeout.InfiniteTimeSpan)
        {
            deadline.CancelAfter(_http.Timeout);
        }
        var requestToken = deadline.Token;
        try
        {
            using var message = new HttpRequestMessage(request is null ? HttpMethod.Get : HttpMethod.Post, new Uri(_address, path));
            if (request is not null)
            {
                message.Content = JsonContent.Create(request);
            }
            using var response = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, requestToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var code = await ReadErrorCodeAsync(response, requestToken).ConfigureAwait(false);
                throw new RendezvousException(path, Classify(code, response.StatusCode), code, response.StatusCode);
            }
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return default;
            }
            const int maximumResponseBytes = RendezvousLimits.MaximumControlResponseBytes;
            if (response.Content.Headers.ContentLength > maximumResponseBytes)
            {
                throw InvalidResponse(path);
            }
            await using var stream = await response.Content.ReadAsStreamAsync(requestToken).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var block = new byte[8192];
            while (true)
            {
                var count = await stream.ReadAsync(block, requestToken).ConfigureAwait(false);
                if (count == 0)
                {
                    break;
                }
                if (buffer.Length + count > maximumResponseBytes)
                {
                    throw InvalidResponse(path);
                }
                buffer.Write(block, 0, count);
            }
            return JsonSerializer.Deserialize<T>(buffer.GetBuffer().AsSpan(0, (int)buffer.Length),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw InvalidResponse(path);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw new RendezvousException(path, RendezvousFailureKind.Timeout, "request_timeout", innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            throw new RendezvousException(path, RendezvousFailureKind.Unreachable, "control_unreachable", innerException: exception);
        }
        catch (IOException exception) when (exception is not RendezvousException)
        {
            throw new RendezvousException(path, RendezvousFailureKind.Unreachable, "control_read_failed", innerException: exception);
        }
        catch (JsonException exception)
        {
            throw InvalidResponse(path, exception);
        }
        catch (NotSupportedException exception)
        {
            throw InvalidResponse(path, exception);
        }
    }

    private static async Task<string> ReadErrorCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var buffer = new byte[4097];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
            length += read;
        }
        if (length is > 0 and <= 4096)
        {
            try
            {
                var error = JsonSerializer.Deserialize<RendezvousErrorResponse>(buffer.AsSpan(0, length),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))?.Error;
                if (error is { Length: > 0 and <= 80 } && error.All(character =>
                        character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))
                {
                    return error;
                }
            }
            catch (JsonException)
            {
            }
        }
        return $"http_{(int)response.StatusCode}";
    }

    private static RendezvousFailureKind Classify(string code, HttpStatusCode status) => code switch
    {
        "invalid_join_code" or "invalid_request_id" or "invalid_session_request" or "invalid_capacity" => RendezvousFailureKind.InvalidRequest,
        "invitation_not_found" or "join_request_expired" => RendezvousFailureKind.InvitationNotFound,
        "invitation_full" => RendezvousFailureKind.InvitationFull,
        "invitation_capacity_reached" or "join_request_capacity_reached" => RendezvousFailureKind.CapacityReached,
        "invitation_unauthorized" or "unauthorized_participant" or "relay_unauthorized" => RendezvousFailureKind.Unauthorized,
        "relay_capacity_reached" or "relay_expired" or "relay_not_found" => RendezvousFailureKind.RelayUnavailable,
        _ => status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => RendezvousFailureKind.Unauthorized,
            HttpStatusCode.TooManyRequests => RendezvousFailureKind.RateLimited,
            >= HttpStatusCode.InternalServerError => RendezvousFailureKind.Unreachable,
            _ => RendezvousFailureKind.Rejected
        }
    };

    private static RendezvousException InvalidResponse(string operation, Exception? exception = null) =>
        new(operation, RendezvousFailureKind.InvalidResponse, "invalid_response", innerException: exception);
}
