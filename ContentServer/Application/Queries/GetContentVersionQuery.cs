using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Queries;

public sealed record GetContentVersionQuery(
    PublisherId PublisherId,
    string Identifier,
    string Version) : IQuery<ContentVersionDto?>;

public sealed class GetContentVersionQueryHandler(
    ContentServerDbContext db
) : IQueryHandler<GetContentVersionQuery, ContentVersionDto?>
{
    public async Task<ContentVersionDto?> Handle(
        GetContentVersionQuery query,
        CancellationToken cancellationToken)
    {
        var normalizedIdentifier = query.Identifier.ToLowerInvariant();
        var content = await db.Contents.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.PublisherId == query.PublisherId &&
                        item.NormalizedIdentifier == normalizedIdentifier,
                cancellationToken);

        if (content is null)
        {
            return null;
        }

        var version = await db.ContentVersions.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.ContentId == content.Id &&
                        item.Version == query.Version,
                cancellationToken);

        if (version is null)
        {
            return null;
        }

        var package = await db.PackageBlobs.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == version.PackageBlobId,
                cancellationToken
            );

        return package is null ? null : ContentVersionDto.From(content, version, package);
    }
}
