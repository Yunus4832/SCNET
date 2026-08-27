using ContentServer.Domain.Contents;
using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Queries;

public sealed record GetPublisherSubmissionQuery(
    PublisherId PublisherId,
    ContentVersionId VersionId) : IQuery<ContentVersionDto?>;

public sealed class GetPublisherSubmissionQueryHandler(ContentServerDbContext db)
    : IQueryHandler<GetPublisherSubmissionQuery, ContentVersionDto?>
{
    public async Task<ContentVersionDto?> Handle(
        GetPublisherSubmissionQuery query,
        CancellationToken cancellationToken)
    {
        var version = await db.ContentVersions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == query.VersionId, cancellationToken);
        if (version is null)
        {
            return null;
        }

        var content = await db.Contents.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == version.ContentId && item.PublisherId == query.PublisherId,
            cancellationToken);
        if (content is null)
        {
            return null;
        }

        var package = await db.PackageBlobs.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == version.PackageBlobId, cancellationToken);
        return package is null ? null : ContentVersionDto.From(content, version, package);
    }
}
