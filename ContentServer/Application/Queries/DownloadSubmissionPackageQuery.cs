using ContentServer.Domain.Contents;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Queries;

public sealed record SubmissionPackageDto(byte[] Data, string FileName, string MediaType);

public sealed record DownloadSubmissionPackageQuery(
    ContentVersionId VersionId) : IQuery<SubmissionPackageDto?>;

public sealed class DownloadSubmissionPackageQueryHandler(
    ContentServerDbContext db
) : IQueryHandler<DownloadSubmissionPackageQuery, SubmissionPackageDto?>
{
    public async Task<SubmissionPackageDto?> Handle(
        DownloadSubmissionPackageQuery query,
        CancellationToken cancellationToken)
    {
        var packageId = await db.ContentVersions.AsNoTracking()
            .Where(version => version.Id == query.VersionId)
            .Select(version => version.PackageBlobId)
            .SingleOrDefaultAsync(cancellationToken);

        if (packageId is null)
        {
            return null;
        }

        return await db.PackageBlobs.AsNoTracking()
            .Where(blob => blob.Id == packageId)
            .Select(blob => new SubmissionPackageDto(blob.Data, blob.FileName, blob.MediaType))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
