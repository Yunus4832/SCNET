using ContentServer.Domain.Administration;
using ContentServer.Domain.Publishers;

using NetCorePal.Extensions.Domain;

using SCNET.Authentication.Protocol.Http;

namespace ContentServer.Domain.AuthenticationServices;

public partial record AuthenticationServiceRegistrationId : IGuidStronglyTypedId;

/// <summary>A reviewed source address. Authentication identities and grants are owned by the service and its clients.</summary>
public sealed class AuthenticationServiceRegistration : Entity<AuthenticationServiceRegistrationId>, IAggregateRoot
{
    private AuthenticationServiceRegistration()
    {
    }

    private readonly List<AuthenticationServiceAuditEntry> _audit = [];

    public IReadOnlyCollection<AuthenticationServiceAuditEntry> Audit => _audit;

    public Deleted Deleted { get; private set; } = new();

    public PublisherId PublisherId { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public string ServiceUrl { get; private set; } = string.Empty;


    public string? Description { get; private set; }

    public AuthenticationServiceRegistrationStatus Status { get; private set; }

    public long Revision { get; private set; }

    public string? ReviewMessage { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public static AuthenticationServiceRegistration Submit(
        PublisherId publisherId,
        string name,
        Uri serviceUrl,
        string? description,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(publisherId);
        var registration = new AuthenticationServiceRegistration { PublisherId = publisherId, CreatedAt = now };
        registration.SetDescription(name, serviceUrl, description);
        registration.Status = AuthenticationServiceRegistrationStatus.Pending;
        registration.RecordChange("Submitted", null, null, now);
        return registration;
    }

    public bool Edit(
        string name,
        Uri serviceUrl,
        string? description,
        long expectedRevision,
        DateTimeOffset now
    )
    {
        if (!Matches(expectedRevision)) { return false; }

        SetDescription(name, serviceUrl, description);
        Status = AuthenticationServiceRegistrationStatus.Pending;
        ReviewMessage = null;
        ReviewedAt = null;
        RecordChange("Edited", null, null, now);
        return true;
    }

    public bool Review(
        bool approved,
        AdministratorId administratorId,
        string? message,
        long expectedRevision,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(administratorId);
        if (!Matches(expectedRevision) || Status != AuthenticationServiceRegistrationStatus.Pending) { return false; }

        message = NormalizeMessage(message);
        Status = approved
            ? AuthenticationServiceRegistrationStatus.Active
            : AuthenticationServiceRegistrationStatus.Rejected;
        ReviewMessage = message;
        ReviewedAt = now;
        RecordChange(approved ? "Approved" : "Rejected", administratorId, message, now);
        return true;
    }

    public bool Withdraw(long expectedRevision, DateTimeOffset now)
    {
        if (!Matches(expectedRevision) || Status == AuthenticationServiceRegistrationStatus.Withdrawn) { return false; }

        Status = AuthenticationServiceRegistrationStatus.Withdrawn;
        RecordChange("Withdrawn", null, null, now);
        return true;
    }

    public bool Disable(AdministratorId administratorId, string? reason, long expectedRevision, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(administratorId);
        if (!Matches(expectedRevision) || Status != AuthenticationServiceRegistrationStatus.Active) { return false; }

        reason = NormalizeMessage(reason);
        Status = AuthenticationServiceRegistrationStatus.Disabled;
        RecordChange("Disabled", administratorId, reason, now);
        return true;
    }

    public bool Delete(AdministratorId? administratorId, long expectedRevision, DateTimeOffset now)
    {
        if (!Matches(expectedRevision)) { return false; }

        Deleted = new Deleted(true);
        RecordChange("Deleted", administratorId, null, now);
        return true;
    }

    private bool Matches(long expectedRevision) => Deleted == new Deleted(false) && Revision == expectedRevision;

    private void SetDescription(string name, Uri serviceUrl, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(serviceUrl);
        var invalidDescription = description is not null &&
                                 description.Any(character =>
                                     char.IsControl(character) && character is not '\n' and not '\r' and not '\t'
                                 );
        if (name.Length > 100 || name.Any(char.IsControl) || description?.Length > 1000 || invalidDescription)
        {
            throw new ArgumentException("Invalid authentication directory description.");
        }

        var uri = AuthenticationServiceEndpoint.ParseBaseUri(serviceUrl.AbsoluteUri);
        Name = name.Trim();
        ServiceUrl = uri.AbsoluteUri;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    private static string? NormalizeMessage(string? message)
    {
        if (message?.Length > 1000) { throw new ArgumentException("Review message exceeds its limit."); }

        return string.IsNullOrWhiteSpace(message) ? null : message.Trim();
    }

    private void RecordChange(string action, AdministratorId? administratorId, string? message, DateTimeOffset now)
    {
        Revision = checked(Revision + 1);
        UpdatedAt = now;
        _audit.Add(new AuthenticationServiceAuditEntry(this, action, administratorId, message, now));
        AddDomainEvent(new AuthenticationServiceChangedDomainEvent(this, action, administratorId, message, now));
    }
}
