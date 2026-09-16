using ContentServer.Domain.Administration;
using ContentServer.Domain.Publishers;

using NetCorePal.Extensions.Domain;

namespace ContentServer.Domain.ServerSources;

public enum ServerSourceRegistrationStatus
{
    Pending,
    Active,
    Rejected
}

public partial record ServerSourceRegistrationId : IGuidStronglyTypedId;

public sealed class ServerSourceRegistration : Entity<ServerSourceRegistrationId>, IAggregateRoot
{
    private ServerSourceRegistration()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string ApiUrl { get; private set; } = string.Empty;

    public PublisherId PublisherId { get; private set; } = null!;

    public string? Description { get; private set; }

    public ServerSourceRegistrationStatus Status { get; private set; }

    public string? ReviewMessage { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public static ServerSourceRegistration Submit(PublisherId publisherId, string name, Uri apiUrl,
        string? description,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(publisherId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(apiUrl);
        if (name.Length > 100 || description?.Length > 1000)
        {
            throw new ArgumentException("Server source registration fields exceed their limits.");
        }

        return new ServerSourceRegistration
        {
            PublisherId = publisherId,
            Name = name.Trim(),
            ApiUrl = apiUrl.AbsoluteUri,
            Description = Normalize(description),
            Status = ServerSourceRegistrationStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public bool Review(bool approved, AdministratorId administratorId, string? message, DateTimeOffset now)
    {
        if (Status != ServerSourceRegistrationStatus.Pending)
        {
            return false;
        }

        Status = approved ? ServerSourceRegistrationStatus.Active : ServerSourceRegistrationStatus.Rejected;
        ReviewMessage = Normalize(message);
        ReviewedAt = now;
        UpdatedAt = now;
        AddDomainEvent(new ServerSourceReviewedDomainEvent(this, administratorId, Status, ReviewMessage, now));
        return true;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record ServerSourceReviewedDomainEvent(ServerSourceRegistration Source,
    AdministratorId AdministratorId, ServerSourceRegistrationStatus Status, string? Message,
    DateTimeOffset OccurredAt) : IDomainEvent;
