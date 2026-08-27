using ContentServer.Domain.Administration;

using NetCorePal.Extensions.Domain;

namespace ContentServer.Domain.Publishers;

public enum PublisherStatus
{
    Pending,
    Active,
    Rejected,
    Suspended
}

public partial record PublisherId : IGuidStronglyTypedId;

public partial record PublisherKeyId : IGuidStronglyTypedId;

public class Publisher : Entity<PublisherId>, IAggregateRoot
{
    private Publisher()
    {
    }

    public string DisplayName { get; private set; } = string.Empty;

    public string Contact { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public PublisherStatus Status { get; private set; }

    public string? ReviewMessage { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public virtual ICollection<PublisherKey> Keys { get; private set; } = [];

    public static Publisher Apply(
        string displayName,
        string contact,
        string? description,
        string keyPrefix,
        string keyHash,
        DateTimeOffset now
    )
    {
        var publisher = new Publisher
        {
            DisplayName = displayName,
            Contact = contact,
            Description = NormalizeOptionalText(description),
            Status = PublisherStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
        publisher.Keys.Add(PublisherKey.Create(keyPrefix, keyHash, now));
        publisher.AddDomainEvent(new PublisherAppliedDomainEvent(publisher));
        return publisher;
    }

    public bool Review(PublisherStatus status, AdministratorId administratorId, string? message, DateTimeOffset now)
    {
        if (Status != PublisherStatus.Pending || status is not (PublisherStatus.Active or PublisherStatus.Rejected))
        {
            return false;
        }

        Status = status;
        ReviewMessage = NormalizeOptionalText(message);
        ReviewedAt = now;
        UpdatedAt = now;
        AddDomainEvent(
            new PublisherStatusChangedDomainEvent(
                this,
                administratorId,
                status,
                ReviewMessage,
                now
            )
        );
        return true;
    }

    public bool Suspend(AdministratorId administratorId, string? message, DateTimeOffset now)
    {
        if (Status != PublisherStatus.Active)
        {
            return false;
        }

        Status = PublisherStatus.Suspended;
        ReviewMessage = NormalizeOptionalText(message);
        UpdatedAt = now;
        AddDomainEvent(new PublisherStatusChangedDomainEvent(
            this, administratorId, Status, ReviewMessage, now));
        return true;
    }

    public bool RevokeKeys(AdministratorId administratorId, DateTimeOffset now)
    {
        var keys = Keys.Where(item => item.RevokedAt is null).ToArray();
        if (keys.Length == 0)
        {
            return false;
        }

        foreach (var key in keys)
        {
            key.Revoke(now);
        }

        AddDomainEvent(new PublisherKeysRevokedDomainEvent(this, administratorId, now));
        return true;
    }

    public bool RestoreKeys(DateTimeOffset now)
    {
        var keys = Keys.Where(item => item.RevokedAt is not null).ToArray();
        if (keys.Length == 0)
        {
            return false;
        }

        foreach (var key in keys)
        {
            key.Restore();
        }

        UpdatedAt = now;
        return true;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public class PublisherKey : Entity<PublisherKeyId>
{
    private PublisherKey()
    {
    }

    public PublisherId PublisherId { get; private set; } = null!;

    public string KeyPrefix { get; private set; } = string.Empty;

    public string KeyHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public virtual Publisher Owner { get; private set; } = null!;

    public static PublisherKey Create(string prefix, string hash, DateTimeOffset now)
    {
        return new PublisherKey { KeyPrefix = prefix, KeyHash = hash, CreatedAt = now };
    }

    public void Touch(DateTimeOffset now)
    {
        LastUsedAt = now;
    }

    public void Revoke(DateTimeOffset now)
    {
        RevokedAt = now;
    }

    public void Restore()
    {
        RevokedAt = null;
    }
}

public sealed record PublisherAppliedDomainEvent(
    Publisher Publisher
) : IDomainEvent;

public sealed record PublisherStatusChangedDomainEvent(
    Publisher Publisher,
    AdministratorId AdministratorId,
    PublisherStatus Status,
    string? Message,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record PublisherKeysRevokedDomainEvent(
    Publisher Publisher,
    AdministratorId AdministratorId,
    DateTimeOffset OccurredAt
) : IDomainEvent;
