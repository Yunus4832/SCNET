using ContentServer.Domain.Administration;
using ContentServer.Domain.Packages;
using ContentServer.Domain.Publishers;

using NetCorePal.Extensions.Domain;

namespace ContentServer.Domain.Contents;

public enum ContentStatus
{
    Active,
    Disabled
}

public enum ContentVersionStatus
{
    Pending,
    Published,
    Rejected
}

public partial record ContentId : IGuidStronglyTypedId;

public partial record ContentVersionId : IGuidStronglyTypedId;

public class ContentItem : Entity<ContentId>, IAggregateRoot
{
    private ContentItem()
    {
    }

    public PublisherId PublisherId { get; private set; } = null!;

    public string Type { get; private set; } = string.Empty;

    public string Identifier { get; private set; } = string.Empty;

    public string NormalizedIdentifier { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Summary { get; private set; }

    public ContentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public virtual List<ContentVersion> Versions { get; private set; } = [];

    public static ContentItem Create(
        PublisherId publisherId,
        string type,
        string identifier,
        string name,
        string? summary,
        DateTimeOffset now
    )
    {
        return new ContentItem
        {
            PublisherId = publisherId,
            Type = type,
            Identifier = identifier,
            NormalizedIdentifier = identifier.ToLowerInvariant(),
            Name = name,
            Summary = NormalizeOptionalText(summary),
            Status = ContentStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public ContentVersion SubmitVersion(
        string version,
        PackageBlobId packageBlobId,
        string packageHash,
        string blobHash,
        string? metadata,
        DateTimeOffset now)
    {
        var item = ContentVersion.Create(PublisherId, Type, Identifier, version, packageBlobId,
            packageHash, blobHash, NormalizeOptionalText(metadata), now);
        Versions.Add(item);
        AddDomainEvent(new ContentVersionSubmittedDomainEvent(this, item));
        return item;
    }

    public void UpdateDetails(string name, string? summary, DateTimeOffset now)
    {
        Name = name.Trim();
        Summary = NormalizeOptionalText(summary);
        UpdatedAt = now;
    }

    public bool ReviewVersion(
        ContentVersionId versionId,
        AdministratorId administratorId,
        ContentVersionStatus status,
        string? message,
        DateTimeOffset now
    )
    {
        var version = Versions.FirstOrDefault(item => item.Id == versionId);
        if (version is null || version.Status != ContentVersionStatus.Pending ||
            status is not (ContentVersionStatus.Published or ContentVersionStatus.Rejected))
        {
            return false;
        }

        var normalizedMessage = NormalizeOptionalText(message);
        version.Review(status, normalizedMessage, now);
        AddDomainEvent(new ContentVersionReviewedDomainEvent(
                this,
                version,
                administratorId,
                status,
                normalizedMessage,
                now
            )
        );
        return true;
    }

    public void SetStatus(ContentStatus status, AdministratorId administratorId, DateTimeOffset now)
    {
        Status = status;
        UpdatedAt = now;
        AddDomainEvent(new ContentStatusChangedDomainEvent(this, administratorId, status, now));
    }

    public void SetStatus(ContentStatus status, DateTimeOffset now)
    {
        Status = status;
        UpdatedAt = now;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public class ContentVersion : Entity<ContentVersionId>
{
    private ContentVersion()
    {
    }

    public ContentId ContentId { get; private set; } = null!;

    public string Version { get; private set; } = string.Empty;

    public PublisherId PublisherId { get; private set; } = null!;

    public string ContentType { get; private set; } = string.Empty;

    public string Identifier { get; private set; } = string.Empty;

    public string PackageHash { get; private set; } = string.Empty;

    public string? BlobHash { get; private set; }

    public PackageBlobId PackageBlobId { get; private set; } = null!;

    public string? MetadataJson { get; private set; }

    public ContentVersionStatus Status { get; private set; }

    public string? ReviewMessage { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public virtual ContentItem Owner { get; private set; } = null!;

    public static ContentVersion Create(
        PublisherId publisherId,
        string contentType,
        string identifier,
        string version,
        PackageBlobId packageBlobId,
        string packageHash,
        string? blobHash,
        string? metadata,
        DateTimeOffset now
    )
    {
        return new ContentVersion
        {
            PublisherId = publisherId,
            ContentType = contentType,
            Identifier = identifier,
            Version = version,
            PackageBlobId = packageBlobId,
            PackageHash = packageHash,
            BlobHash = blobHash,
            MetadataJson = metadata,
            Status = ContentVersionStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Review(ContentVersionStatus status, string? message, DateTimeOffset now)
    {
        Status = status;
        ReviewMessage = message;
        ReviewedAt = now;
        UpdatedAt = now;
        if (status == ContentVersionStatus.Published)
        {
            PublishedAt = now;
        }
    }
}

public sealed record ContentVersionSubmittedDomainEvent(
    ContentItem Content,
    ContentVersion Version
) : IDomainEvent;

public sealed record ContentVersionReviewedDomainEvent(
    ContentItem Content,
    ContentVersion Version,
    AdministratorId AdministratorId,
    ContentVersionStatus Status,
    string? Message,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record ContentStatusChangedDomainEvent(
    ContentItem Content,
    AdministratorId AdministratorId,
    ContentStatus Status,
    DateTimeOffset OccurredAt
) : IDomainEvent;
