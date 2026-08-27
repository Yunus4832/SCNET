using ContentServer.Domain.Contents;
using ContentServer.Domain.Packages;
using ContentServer.Domain.Publishers;
using ContentServer.Extensions;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Queries;

public enum VersionQueryScope
{
    Public,
    All,
    Publisher
}

public sealed record ContentVersionDto(
    string ContentId,
    string PublisherId,
    string Type,
    string Identifier,
    string Name,
    string? Summary,
    ContentStatus ContentStatus,
    string VersionId,
    string Version,
    string PackageHash,
    long PackageSize,
    string FileName,
    string? MetadataJson,
    ContentVersionStatus Status,
    string? ReviewMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt)
{
    public static ContentVersionDto From(ContentItem content, ContentVersion version, PackageBlob blob)
    {
        return new ContentVersionDto(
            content.Id.ToString(),
            content.PublisherId.ToString(),
            content.Type,
            content.Identifier,
            content.Name,
            content.Summary,
            content.Status,
            version.Id.ToString(),
            version.Version,
            blob.Hash,
            blob.Size,
            blob.FileName,
            version.MetadataJson,
            version.Status,
            version.ReviewMessage,
            version.CreatedAt,
            version.PublishedAt
        );
    }
}

public sealed record ListVersionsQuery(
    VersionQueryScope Scope,
    PageRequest Page,
    string? Type = null,
    string? Identifier = null,
    string? Version = null,
    string? Search = null,
    PublisherId? PublisherId = null,
    ContentVersionStatus? Status = null,
    ContentId? ContentId = null,
    bool LatestOnly = false) : IQuery<PagedData<ContentVersionDto>>;

public sealed class ListVersionsQueryHandler(
    ContentServerDbContext db
) : IQueryHandler<ListVersionsQuery, PagedData<ContentVersionDto>>
{
    public async Task<PagedData<ContentVersionDto>> Handle(
        ListVersionsQuery request,
        CancellationToken cancellationToken)
    {
        var normalizedIdentifier = request.Identifier?.ToLowerInvariant();
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var contents = db.Contents
            .AsNoTracking()
            .WhereIf(
                request.Scope == VersionQueryScope.Public,
                content => content.Status == ContentStatus.Active)
            .WhereIf(
                request.Scope == VersionQueryScope.Publisher,
                content => content.PublisherId == request.PublisherId)
            .WhereIf(
                request.ContentId is not null,
                content => content.Id == request.ContentId)
            .WhereIf(
                !string.IsNullOrWhiteSpace(request.Type),
                content => content.Type == request.Type)
            .WhereIf(
                !string.IsNullOrWhiteSpace(normalizedIdentifier),
                content => content.NormalizedIdentifier == normalizedIdentifier)
            .WhereIf(
                search is not null,
                content => content.Name.Contains(search!) ||
                           content.Identifier.Contains(search!) ||
                           content.Summary != null && content.Summary.Contains(search!));

        var contentIds = await contents.Select(content => content.Id).ToListAsync(cancellationToken);
        var versions = db.ContentVersions.AsNoTracking()
            .WhereIf(
                request.Scope == VersionQueryScope.Public,
                version => version.Status == ContentVersionStatus.Published)
            .WhereIf(
                request.Status is not null,
                version => version.Status == request.Status)
            .WhereIf(
                !string.IsNullOrWhiteSpace(request.Version),
                version => version.Version == request.Version)
            .Where(version => contentIds.Contains(version.ContentId))
            .WhereIf(
                request.LatestOnly,
                version => version.Id == db.ContentVersions
                    .Where(other => other.ContentId == version.ContentId &&
                                    other.Status == ContentVersionStatus.Published)
                    .OrderByDescending(other => other.Id)
                    .Select(other => other.Id)
                    .First());

        var page = await versions
            .OrderByDescending(version => version.Id)
            .ToPagedDataAsync(request.Page, cancellationToken);

        var pageContentIds = page.Items
            .Select(version => version.ContentId)
            .Distinct()
            .ToArray();

        var packageIds = page.Items
            .Select(version => version.PackageBlobId)
            .Distinct()
            .ToArray();

        var contentById = await db.Contents
            .AsNoTracking()
            .Where(content => ((IEnumerable<ContentId>)pageContentIds).Contains(content.Id))
            .ToDictionaryAsync(content => content.Id, cancellationToken);

        var packageById = await db.PackageBlobs
            .AsNoTracking()
            .Where(package => ((IEnumerable<PackageBlobId>)packageIds).Contains(package.Id))
            .ToDictionaryAsync(package => package.Id, cancellationToken);

        var items = page.Items.Select(version => ContentVersionDto.From(
                contentById[version.ContentId],
                version,
                packageById[version.PackageBlobId]))
            .ToArray();

        return new PagedData<ContentVersionDto>(items, page.Total, page.PageIndex, page.PageSize);
    }
}
