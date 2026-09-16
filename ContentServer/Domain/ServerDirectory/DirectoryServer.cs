using System.Text.Json;

using ContentServer.Domain.Administration;
using ContentServer.Domain.Publishers;

using NetCorePal.Extensions.Domain;

namespace ContentServer.Domain.ServerDirectory;

public enum DirectoryServerReviewStatus
{
    Pending,
    Approved,
    Rejected
}

public partial record DirectoryServerId : IGuidStronglyTypedId;

public sealed class DirectoryServer : Entity<DirectoryServerId>, IAggregateRoot
{
    private DirectoryServer()
    {
    }

    public PublisherId PublisherId { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public string Address { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string TagsJson { get; private set; } = "[]";

    public DirectoryServerReviewStatus ReviewStatus { get; private set; }

    public string? ReviewMessage { get; private set; }

    public bool IsEnabledByPublisher { get; private set; }

    public DateTimeOffset? SuspendedAt { get; private set; }

    public string? SuspensionReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public IReadOnlyList<string> Tags => JsonSerializer.Deserialize<string[]>(TagsJson) ?? [];

    public static DirectoryServer Submit(PublisherId publisherId, string name, string address,
        string? description, IReadOnlyList<string> tags, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(publisherId);
        ValidateFields(name, address, description, tags);

        return new DirectoryServer
        {
            PublisherId = publisherId,
            Name = name.Trim(),
            Address = address.Trim(),
            Description = Normalize(description),
            TagsJson = JsonSerializer.Serialize(tags.Select(tag => tag.Trim()).Distinct().ToArray()),
            ReviewStatus = DirectoryServerReviewStatus.Pending,
            IsEnabledByPublisher = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public bool Review(bool approved, AdministratorId administratorId, string? message, DateTimeOffset now)
    {
        if (ReviewStatus != DirectoryServerReviewStatus.Pending)
        {
            return false;
        }

        ReviewStatus = approved ? DirectoryServerReviewStatus.Approved : DirectoryServerReviewStatus.Rejected;
        ReviewMessage = Normalize(message);
        ReviewedAt = now;
        UpdatedAt = now;
        AddDomainEvent(new DirectoryServerReviewedDomainEvent(this, administratorId, ReviewStatus, ReviewMessage,
            now));
        return true;
    }

    public void Update(string name, string address, string? description, IReadOnlyList<string> tags,
        bool requiresReview, DateTimeOffset now)
    {
        ValidateFields(name, address, description, tags);
        Name = name.Trim();
        Address = address.Trim();
        Description = Normalize(description);
        TagsJson = JsonSerializer.Serialize(tags.Select(tag => tag.Trim()).Distinct().ToArray());
        UpdatedAt = now;
        if (requiresReview)
        {
            ReviewStatus = DirectoryServerReviewStatus.Pending;
            ReviewMessage = null;
            ReviewedAt = null;
        }
    }

    public void SetPublisherEnabled(bool enabled, DateTimeOffset now)
    {
        IsEnabledByPublisher = enabled;
        UpdatedAt = now;
    }

    public bool Suspend(AdministratorId administratorId, string? reason, DateTimeOffset now)
    {
        if (SuspendedAt is not null)
        {
            return false;
        }

        SuspendedAt = now;
        SuspensionReason = Normalize(reason);
        UpdatedAt = now;
        AddDomainEvent(new DirectoryServerSuspensionChangedDomainEvent(this, administratorId, true,
            SuspensionReason, now));
        return true;
    }

    public bool Restore(AdministratorId administratorId, DateTimeOffset now)
    {
        if (SuspendedAt is null)
        {
            return false;
        }

        SuspendedAt = null;
        SuspensionReason = null;
        UpdatedAt = now;
        AddDomainEvent(new DirectoryServerSuspensionChangedDomainEvent(this, administratorId, false, null, now));
        return true;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateFields(string name, string address, string? description, IReadOnlyList<string> tags)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentNullException.ThrowIfNull(tags);
        if (name.Length > 100 || address.Length > 255 || description?.Length > 1024 || tags.Count > 16 ||
            tags.Any(tag => string.IsNullOrWhiteSpace(tag) || tag.Length > 32))
        {
            throw new ArgumentException("Directory server fields exceed their limits.");
        }
    }
}

public sealed record DirectoryServerReviewedDomainEvent(DirectoryServer Server, AdministratorId AdministratorId,
    DirectoryServerReviewStatus Status, string? Message, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record DirectoryServerSuspensionChangedDomainEvent(DirectoryServer Server,
    AdministratorId AdministratorId, bool Suspended, string? Reason, DateTimeOffset OccurredAt) : IDomainEvent;
