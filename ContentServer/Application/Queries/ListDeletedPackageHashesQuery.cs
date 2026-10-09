using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Domain;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Queries;

public sealed record ListDeletedPackageHashesQuery : IQuery<IReadOnlyList<string>>;

public sealed class ListDeletedPackageHashesQueryHandler(ContentServerDbContext db)
    : IQueryHandler<ListDeletedPackageHashesQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(ListDeletedPackageHashesQuery query,
        CancellationToken cancellationToken)
    {
        return await db.PackageBlobs.WhereDeleted(new Deleted(true))
            .Select(package => package.Hash)
            .ToListAsync(cancellationToken);
    }
}
