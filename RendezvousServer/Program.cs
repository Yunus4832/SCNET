using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.HttpOverrides;

using Rendezvous.Protocol;

using RendezvousServer;
using RendezvousServer.Relay;
using RendezvousServer.Sessions;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = RendezvousLimits.MaximumControlRequestBytes;
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    foreach (var proxy in builder.Configuration.GetSection("Rendezvous:TrustedProxies").Get<string[]>() ?? ["127.0.0.1", "::1"])
    {
        options.KnownProxies.Add(IPAddress.Parse(proxy));
    }
});
var udpPerAddress = builder.Configuration.GetValue("Rendezvous:UdpRequestsPerAddressPerSecond", 120);
var udpTotal = builder.Configuration.GetValue("Rendezvous:UdpRequestsPerSecond", 4096);
if (udpPerAddress is < 1 or > 1_000_000 || udpTotal < udpPerAddress || udpTotal > 1_000_000)
{
    throw new InvalidOperationException("Invalid UDP request budget.");
}
var ticketLifetimeSeconds = builder.Configuration.GetValue("Rendezvous:TicketLifetimeSeconds", 30);
var registrationLifetimeSeconds = builder.Configuration.GetValue("Rendezvous:RegistrationLifetimeSeconds", 60);
var relayLifetimeSeconds = builder.Configuration.GetValue("Rendezvous:RelayLifetimeSeconds", 60);
var relayBytesPerSecond = builder.Configuration.GetValue("Rendezvous:RelayBytesPerSecond", 1024 * 1024);
if (ticketLifetimeSeconds is < 1 or > 300 || registrationLifetimeSeconds is < 60 or > 3600 ||
    relayLifetimeSeconds is < 60 or > 3600 || relayBytesPerSecond is < 2048 or > 1_073_741_824)
{
    throw new InvalidOperationException("Invalid ticket, registration, relay lifetime or relay traffic budget.");
}
var invitationLifetimeSeconds = builder.Configuration.GetValue("Rendezvous:InvitationLifetimeSeconds", 300);
var invitationCapacity = builder.Configuration.GetValue("Rendezvous:InvitationCapacity", 1000);
if (invitationLifetimeSeconds is < 30 or > 86400 || invitationCapacity is < 1 or > 1_000_000)
{
    throw new InvalidOperationException("Invitation lifetime must be 30–86400 seconds and capacity 1–1000000.");
}
builder.Services.AddSingleton(new InvitationRegistry(TimeProvider.System,
    TimeSpan.FromSeconds(invitationLifetimeSeconds), invitationCapacity, ticketLifetimeSeconds));
builder.Services.AddHostedService<UdpSignalHost>();
var relayAddress = IPAddress.Parse(builder.Configuration.GetValue("Rendezvous:RelayAdvertisedAddress", "127.0.0.1")!);
var relayBindAddress = relayAddress.AddressFamily == AddressFamily.InterNetworkV6
    ? IPAddress.IPv6Any
    : IPAddress.Any;
builder.Services.AddSingleton(provider => new RelayRouteRegistry(provider.GetRequiredService<InvitationRegistry>(),
    TimeProvider.System, relayBindAddress, relayAddress,
    builder.Configuration.GetValue("Rendezvous:RelayFirstPort", 32000),
    builder.Configuration.GetValue("Rendezvous:RelayPortCount", 128), relayLifetimeSeconds, relayBytesPerSecond,
    provider.GetRequiredService<ILoggerFactory>()));
builder.Services.AddHostedService<RelayCleanupHost>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}:{(context.Request.Path == "/rooms" ? "directory" : "control")}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("Rendezvous:HttpRequestsPerMinute", 600),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
var app = builder.Build();
app.UseForwardedHeaders();
app.UseRateLimiter();
app.MapGet("/transport", () => Results.Ok(new RendezvousDescriptor(
    builder.Configuration.GetValue("Rendezvous:UdpPort", 31990),
    builder.Configuration.GetValue<string>("Rendezvous:UdpAdvertisedHost"), "rooms")));
var control = app.MapGroup(string.Empty).AddEndpointFilter<SignalControlLogFilter>();
app.MapGet("/rooms", (int? limit, string? cursor, InvitationRegistry registry) =>
{
    try
    {
        return Results.Ok(registry.ListRooms(limit ?? RendezvousLimits.MaximumRoomPageSize, cursor));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new RendezvousErrorResponse(exception.ParamName == "cursor"
            ? "invalid_room_cursor" : "invalid_room_limit"));
    }
});
control.MapPost("/invitations/update-room", (UpdateRoomRequest request, InvitationRegistry registry) =>
{
    try
    {
        registry.UpdateRoom(request.SessionId, request.Credential, request.Publication);
        return Results.NoContent();
    }
    catch (ArgumentException)
    {
        return Results.BadRequest(new RendezvousErrorResponse("invalid_room_publication"));
    }
    catch (InvalidOperationException)
    {
        return Results.Unauthorized();
    }
});

control.MapPost("/invitations/join-session", (JoinSessionRequest request, InvitationRegistry registry) =>
{
    try
    {
        return Results.Ok(registry.JoinSession(request.SessionId, request.RequestId));
    }
    catch (InvalidOperationException exception)
    {
        return exception.Message switch
        {
            "invalid_session_request" => Results.BadRequest(new RendezvousErrorResponse(exception.Message)),
            "invitation_not_found" => Results.NotFound(new RendezvousErrorResponse(exception.Message)),
            _ => Results.Conflict(new RendezvousErrorResponse(exception.Message))
        };
    }
});
control.MapPost("/invitations/create", (CreateInvitationRequest request, InvitationRegistry registry) =>
{
    try
    {
        return Results.Ok(registry.Create(request.MaxParticipants));
    }
    catch (ArgumentOutOfRangeException)
    {
        return Results.BadRequest(new RendezvousErrorResponse("invalid_capacity"));
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new RendezvousErrorResponse(exception.Message));
    }
});
control.MapPost("/invitations/join", (JoinInvitationRequest request, InvitationRegistry registry) =>
{
    try
    {
        return Results.Ok(registry.Join(request.JoinCode, request.RequestId));
    }
    catch (InvalidOperationException exception)
    {
        return exception.Message switch
        {
            "invalid_join_code" or "invalid_request_id" => Results.BadRequest(new RendezvousErrorResponse(exception.Message)),
            "invitation_not_found" => Results.NotFound(new RendezvousErrorResponse(exception.Message)),
            _ => Results.Conflict(new RendezvousErrorResponse(exception.Message))
        };
    }
});
control.MapPost("/invitations/leave", (RegistrationTicketRequest request, InvitationRegistry registry) =>
{
    try
    {
        registry.Leave(request.SessionId, request.ParticipantId, request.Credential);
        return Results.NoContent();
    }
    catch (InvalidOperationException)
    {
        return Results.Unauthorized();
    }
});
control.MapPost("/invitations/renew-participant", (RegistrationTicketRequest request, InvitationRegistry registry) =>
{
    try
    {
        return Results.Ok(new InvitationRenewal(registry.RenewParticipant(request.SessionId, request.ParticipantId, request.Credential)));
    }
    catch (InvalidOperationException)
    {
        return Results.Unauthorized();
    }
});
control.MapPost("/relay/renew-preparation", (RelayAllocateRequest request, RelayRouteRegistry routes) =>
{
    try
    {
        return Results.Ok(routes.RenewPreparation(request.SessionId, request.ParticipantId, request.AttemptId, request.Credential));
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new RendezvousErrorResponse(exception.Message));
    }
});
control.MapPost("/invitations/renew", (HostInvitationRequest request, InvitationRegistry registry) =>
{
    try
    {
        return Results.Ok(new { ExpiresAt = registry.Renew(request.SessionId, request.Credential) });
    }
    catch (InvalidOperationException)
    {
        return Results.Unauthorized();
    }
});
control.MapPost("/invitations/close", (HostInvitationRequest request, InvitationRegistry registry) =>
{
    try
    {
        registry.Close(request.SessionId, request.Credential);
        return Results.NoContent();
    }
    catch (InvalidOperationException)
    {
        return Results.Unauthorized();
    }
});
control.MapPost("/invitations/registration-ticket", (RegistrationTicketRequest request, InvitationRegistry registry) =>
{
    try
    {
        return Results.Ok(registry.IssueTicket(request.SessionId, request.ParticipantId, request.Credential));
    }
    catch (InvalidOperationException)
    {
        return Results.Unauthorized();
    }
});
control.MapPost("/relay/allocate", (RelayAllocateRequest request, RelayRouteRegistry routes) =>
{
    try
    {
        return Results.Ok(routes.Allocate(request.SessionId, request.ParticipantId, request.AttemptId, request.Credential));
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new RendezvousErrorResponse(exception.Message));
    }
});
control.MapPost("/relay/host-offers", async (HostInvitationRequest request, RelayRouteRegistry routes, CancellationToken token) =>
{
    try
    {
        var until = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(20);
        do
        {
            var offers = routes.GetHostOffers(request.SessionId, request.Credential);
            if (offers.Count > 0)
            {
                return Results.Ok(offers);
            }
            await Task.Delay(250, token);
        } while (DateTimeOffset.UtcNow < until);
        return Results.Ok(Array.Empty<RelayAllocation>());
    }
    catch (InvalidOperationException)
    {
        return Results.Unauthorized();
    }
});
control.MapPost("/relay/host-ready", (HostRelayReadyRequest request, RelayRouteRegistry routes) =>
{
    try
    {
        routes.AcknowledgeHost(request.SessionId, request.ParticipantId, request.AttemptId, request.Credential, request.ExpiresAt);
        return Results.NoContent();
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new RendezvousErrorResponse(exception.Message));
    }
});
app.MapGet("/health", () => Results.Ok(new { Status = "ready" }));
await app.RunAsync();

internal sealed record CreateInvitationRequest(int MaxParticipants = 8);
internal sealed record JoinInvitationRequest(string JoinCode, Guid RequestId);
internal sealed record HostInvitationRequest(Guid SessionId, string Credential);

internal sealed record RegistrationTicketRequest(Guid SessionId, Guid ParticipantId, string Credential);

internal sealed record RelayAllocateRequest(Guid SessionId, Guid ParticipantId, Guid AttemptId, string Credential);

internal sealed record JoinSessionRequest(Guid SessionId, Guid RequestId);

internal sealed record HostRelayReadyRequest(Guid SessionId, Guid ParticipantId, Guid AttemptId, string Credential, DateTimeOffset ExpiresAt);

internal sealed record UpdateRoomRequest(Guid SessionId, string Credential, RoomPublication Publication);
