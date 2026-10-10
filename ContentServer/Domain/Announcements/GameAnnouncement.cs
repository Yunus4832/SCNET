using NetCorePal.Extensions.Domain;

namespace ContentServer.Domain.Announcements;

public enum AnnouncementStatus
{
    Draft,
    Published,
    Withdrawn
}

public partial record GameAnnouncementId : IGuidStronglyTypedId;

public sealed class GameAnnouncement : Entity<GameAnnouncementId>, IAggregateRoot
{
    private GameAnnouncement()
    {
    }

    public Deleted Deleted { get; private set; } = new(false);

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public AnnouncementStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public static GameAnnouncement Create(string title, string body, DateTimeOffset now)
    {
        Validate(title, body);
        return new GameAnnouncement
        {
            Title = title.Trim(),
            Body = body.Trim(),
            Status = AnnouncementStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Edit(string title, string body, DateTimeOffset now)
    {
        if (Status != AnnouncementStatus.Draft)
        {
            throw new InvalidOperationException("Only draft announcements can be edited.");
        }

        Validate(title, body);
        Title = title.Trim();
        Body = body.Trim();
        UpdatedAt = now;
    }

    public void Publish(DateTimeOffset now)
    {
        if (Status != AnnouncementStatus.Draft)
        {
            throw new InvalidOperationException("Only draft announcements can be published.");
        }

        Status = AnnouncementStatus.Published;
        PublishedAt = now;
        UpdatedAt = now;
    }

    public void Withdraw(DateTimeOffset now)
    {
        if (Status != AnnouncementStatus.Published)
        {
            throw new InvalidOperationException("Only published announcements can be withdrawn.");
        }

        Status = AnnouncementStatus.Withdrawn;
        UpdatedAt = now;
    }

    private static void Validate(string title, string body)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 120 ||
            string.IsNullOrWhiteSpace(body) || body.Length > 10000)
        {
            throw new ArgumentException("Invalid announcement title or body.");
        }
    }
}
