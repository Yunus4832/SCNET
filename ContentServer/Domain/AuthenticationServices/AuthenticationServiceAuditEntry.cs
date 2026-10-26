using ContentServer.Domain.Administration;

namespace ContentServer.Domain.AuthenticationServices;

/// <summary>Immutable public listing snapshot; null administrator denotes its owning publisher.</summary>
public sealed class AuthenticationServiceAuditEntry
{
    private AuthenticationServiceAuditEntry()
    {
    }

    internal AuthenticationServiceAuditEntry(
        AuthenticationServiceRegistration registration,
        string action,
        AdministratorId? administratorId,
        string? message,
        DateTimeOffset now
    )
    {
        Revision = registration.Revision;
        Action = action;
        AdministratorId = administratorId;
        Message = message;
        OccurredAt = now;
        Name = registration.Name;
        ServiceUrl = registration.ServiceUrl;
        Description = registration.Description;
        Status = registration.Status;
    }

    public long Revision { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public AdministratorId? AdministratorId { get; private set; }

    public string? Message { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string ServiceUrl { get; private set; } = string.Empty;


    public string? Description { get; private set; }

    public AuthenticationServiceRegistrationStatus Status { get; private set; }
}
