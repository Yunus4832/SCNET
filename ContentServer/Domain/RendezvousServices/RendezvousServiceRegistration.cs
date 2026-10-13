using ContentServer.Domain.Administration;
using ContentServer.Domain.Publishers;

using NetCorePal.Extensions.Domain;

namespace ContentServer.Domain.RendezvousServices;

public enum RendezvousServiceRegistrationStatus
{
    Pending,
    Active,
    Rejected,
    Disabled
}

public partial record RendezvousServiceRegistrationId : IGuidStronglyTypedId;

public sealed class RendezvousServiceRegistration : Entity<RendezvousServiceRegistrationId>, IAggregateRoot
{
    private RendezvousServiceRegistration()
    {
    }

    public Deleted Deleted { get; private set; } = new(false);

    public string Name { get; private set; } = string.Empty;

    public string ControlUrl { get; private set; } = string.Empty;

    public PublisherId PublisherId { get; private set; } = null!;

    public string? Description { get; private set; }

    public RendezvousServiceRegistrationStatus Status { get; private set; }

    public string? ReviewMessage { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public static RendezvousServiceRegistration Submit(PublisherId publisherId, string name, Uri controlUrl,
        string? description,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(publisherId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(controlUrl);
        if (name.Length > 100 || description?.Length > 1000)
        {
            throw new ArgumentException("Rendezvous service registration fields exceed their limits.");
        }

        var registration = new RendezvousServiceRegistration
        {
            PublisherId = publisherId,
            Name = name.Trim(),
            ControlUrl = controlUrl.AbsoluteUri,
            Description = Normalize(description),
            Status = RendezvousServiceRegistrationStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
        registration.AddDomainEvent(new RendezvousServiceChangedDomainEvent(registration, "submitted", now));
        return registration;
    }

    public bool Review(bool approved, AdministratorId administratorId, string? message, DateTimeOffset now)
    {
        if (Status != RendezvousServiceRegistrationStatus.Pending)
        {
            return false;
        }

        Status = approved ? RendezvousServiceRegistrationStatus.Active : RendezvousServiceRegistrationStatus.Rejected;
        ReviewMessage = Normalize(message);
        ReviewedAt = now;
        UpdatedAt = now;
        AddDomainEvent(new RendezvousServiceReviewedDomainEvent(this, administratorId, Status, ReviewMessage, now));
        return true;
    }

    public void Edit(string name, Uri controlUrl, string? description, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(controlUrl);
        if (name.Length > 100 || description?.Length > 1000)
        {
            throw new ArgumentException("Rendezvous service registration fields exceed their limits.");
        }
        Name = name.Trim();
        ControlUrl = controlUrl.AbsoluteUri;
        Description = Normalize(description);
        Status = RendezvousServiceRegistrationStatus.Pending;
        ReviewMessage = null;
        ReviewedAt = null;
        UpdatedAt = now;
        AddDomainEvent(new RendezvousServiceChangedDomainEvent(this, "edited", now));
    }

    public bool Disable(DateTimeOffset now)
    {
        if (Status != RendezvousServiceRegistrationStatus.Active)
        {
            return false;
        }
        Status = RendezvousServiceRegistrationStatus.Disabled;
        UpdatedAt = now;
        AddDomainEvent(new RendezvousServiceChangedDomainEvent(this, "disabled", now));
        return true;
    }

    public void Delete(DateTimeOffset now)
    {
        if (Deleted == new Deleted(true))
        {
            return;
        }
        Deleted = new Deleted(true);
        UpdatedAt = now;
        AddDomainEvent(new RendezvousServiceChangedDomainEvent(this, "deleted", now));
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record RendezvousServiceReviewedDomainEvent(RendezvousServiceRegistration Source,
    AdministratorId AdministratorId, RendezvousServiceRegistrationStatus Status, string? Message,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record RendezvousServiceChangedDomainEvent(RendezvousServiceRegistration Service, string Action,
    DateTimeOffset OccurredAt) : IDomainEvent;
