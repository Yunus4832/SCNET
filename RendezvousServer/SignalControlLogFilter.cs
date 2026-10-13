using Rendezvous.Protocol;

namespace RendezvousServer;

/// <summary>Logs control operation identity without serializing requests or credentials.</summary>
internal sealed class SignalControlLogFilter(ILogger<SignalControlLogFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var session = Guid.Empty;
        var participant = Guid.Empty;
        var attempt = Guid.Empty;
        foreach (var argument in context.Arguments)
        {
            switch (argument)
            {
                case JoinInvitationRequest request:
                    attempt = request.RequestId;
                    break;
                case JoinSessionRequest request:
                    session = request.SessionId;
                    attempt = request.RequestId;
                    break;
                case UpdateRoomRequest request:
                    session = request.SessionId;
                    break;
                case HostInvitationRequest request:
                    session = request.SessionId;
                    break;
                case RegistrationTicketRequest request:
                    session = request.SessionId;
                    participant = request.ParticipantId;
                    break;
                case RelayAllocateRequest request:
                    session = request.SessionId;
                    participant = request.ParticipantId;
                    attempt = request.AttemptId;
                    break;
                case HostRelayReadyRequest request:
                    session = request.SessionId;
                    participant = request.ParticipantId;
                    attempt = request.AttemptId;
                    break;
            }
        }
        var result = await next(context);
        var value = (result as IValueHttpResult)?.Value;
        switch (value)
        {
            case SessionRegistration registration:
                session = registration.SessionId;
                break;
            case ParticipantRegistration registration:
                session = registration.SessionId;
                participant = registration.ParticipantId;
                break;
        }
        logger.LogDebug(
            "Control signaling {Operation}: trace {TraceId}, session {SessionId}, participant {ParticipantId}, attempt {AttemptId}, status {StatusCode}, error {ErrorCode}",
            context.HttpContext.Request.Path.Value, context.HttpContext.TraceIdentifier,
            session, participant, attempt, (result as IStatusCodeHttpResult)?.StatusCode ?? 200,
            (value as RendezvousErrorResponse)?.Error);
        return result;
    }
}
