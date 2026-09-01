using NetCorePal.Extensions.Domain;

namespace ContentServer.Domain.Administration;

public enum AdministratorStatus
{
    Pending,
    Active,
    Rejected,
    Suspended
}

public partial record AdministratorId : IGuidStronglyTypedId;

public partial record AdministratorKeyId : IGuidStronglyTypedId;

public class Administrator : Entity<AdministratorId>, IAggregateRoot
{
    private Administrator()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public AdministratorStatus Status { get; private set; }

    public bool IsSuperAdministrator { get; private set; }

    public string Contact { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? ReviewMessage { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public virtual ICollection<AdministratorKey> Keys { get; private set; } = [];

    public static Administrator Create(
        string name,
        string keyPrefix,
        string keyHash,
        DateTimeOffset now,
        bool isSuperAdministrator = false)
    {
        var administrator = new Administrator
        {
            Name = name.Trim(),
            Status = AdministratorStatus.Active,
            IsSuperAdministrator = isSuperAdministrator,
            CreatedAt = now,
            UpdatedAt = now
        };
        administrator.Keys.Add(AdministratorKey.Create(keyPrefix, keyHash, now));
        return administrator;
    }

    public static Administrator Apply(string name, string contact, string? description,
        string keyPrefix, string keyHash, DateTimeOffset now)
    {
        var administrator = new Administrator
        {
            Name = name.Trim(),
            Contact = contact.Trim(),
            Description = Normalize(description),
            Status = AdministratorStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
        administrator.Keys.Add(AdministratorKey.Create(keyPrefix, keyHash, now));
        return administrator;
    }

    public bool Review(AdministratorStatus status, AdministratorId reviewerId, string? message, DateTimeOffset now)
    {
        if (Status != AdministratorStatus.Pending ||
            status is not (AdministratorStatus.Active or AdministratorStatus.Rejected))
        {
            return false;
        }

        Status = status;
        ReviewMessage = Normalize(message);
        ReviewedAt = now;
        UpdatedAt = now;
        AddDomainEvent(new AdministratorStatusChangedDomainEvent(this, reviewerId, status, ReviewMessage, now));
        return true;
    }

    public bool RevokeKeys(DateTimeOffset now)
    {
        if (IsSuperAdministrator)
        {
            return false;
        }

        var keys = Keys.Where(key => key.RevokedAt is null).ToArray();
        if (keys.Length == 0)
        {
            return false;
        }

        foreach (var key in keys)
        {
            key.Revoke(now);
        }

        return true;
    }

    public bool RestoreKeys(DateTimeOffset now)
    {
        if (IsSuperAdministrator)
        {
            return false;
        }

        var keys = Keys.Where(key => key.RevokedAt is not null).ToArray();
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

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record AdministratorStatusChangedDomainEvent(
    Administrator Administrator,
    AdministratorId ReviewerId,
    AdministratorStatus Status,
    string? Message,
    DateTimeOffset OccurredAt) : IDomainEvent;

public class AdministratorKey : Entity<AdministratorKeyId>
{
    private AdministratorKey()
    {
    }

    public AdministratorId AdministratorId { get; private set; } = null!;

    public string KeyPrefix { get; private set; } = string.Empty;

    public string KeyHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public virtual Administrator Owner { get; private set; } = null!;

    public static AdministratorKey Create(string prefix, string hash, DateTimeOffset now)
    {
        return new AdministratorKey
        {
            KeyPrefix = prefix,
            KeyHash = hash,
            CreatedAt = now
        };
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
