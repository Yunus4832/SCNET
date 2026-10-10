using ContentServer.Domain.Releases;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Queries;

public sealed record ListGameReleasesQuery(bool PublicOnly, string? Platform = null)
    : IQuery<IReadOnlyList<GameRelease>>;

public sealed class ListGameReleasesQueryHandler(ContentServerDbContext db)
    : IQueryHandler<ListGameReleasesQuery, IReadOnlyList<GameRelease>>
{
    public async Task<IReadOnlyList<GameRelease>> Handle(ListGameReleasesQuery request,
        CancellationToken cancellationToken)
    {
        IQueryable<GameRelease> query = db.GameReleases.AsNoTracking().Include(item => item.Artifacts);
        if (request.PublicOnly)
        {
            query = query.Where(item => item.Status == GameReleaseStatus.Published);
        }

        if (request.Platform is not null)
        {
            query = query.Where(item => item.Artifacts.Any(artifact => artifact.Platform == request.Platform));
        }

        var items = await query.ToArrayAsync(cancellationToken);
        return request.PublicOnly
            ? items.OrderByDescending(item => System.Version.Parse(item.Version)).ToArray()
            : items.OrderByDescending(item => item.CreatedAt).ToArray();
    }
}
