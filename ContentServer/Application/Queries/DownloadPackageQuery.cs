using ContentServer.Domain.Contents;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Queries;

public sealed record PackageDownloadDto(string Hash, string FileName, string MediaType);

public sealed record DownloadPackageQuery(string Hash) : IQuery<PackageDownloadDto?>;

public sealed class DownloadPackageQueryHandler(
    ContentServerDbContext db
) : IQueryHandler<DownloadPackageQuery, PackageDownloadDto?>
{
    public async Task<PackageDownloadDto?> Handle(
        DownloadPackageQuery query,
        CancellationToken cancellationToken)
    {
        var blob = await db.PackageBlobs.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Hash == query.Hash.ToLower(), cancellationToken);
        if (blob is null)
        {
            return null;
        }

        var contentIds = await db.ContentVersions.AsNoTracking()
            .Where(version => version.PackageBlobId == blob.Id &&
                              version.Status == ContentVersionStatus.Published)
            .Select(version => version.ContentId)
            .ToListAsync(cancellationToken);

        var isPublic = await db.Contents.AsNoTracking()
            .AnyAsync(content => contentIds.Contains(content.Id) &&
                                 content.Status == ContentStatus.Active, cancellationToken);
        return isPublic ? new PackageDownloadDto(blob.Hash, blob.FileName, blob.MediaType) : null;
    }
}
