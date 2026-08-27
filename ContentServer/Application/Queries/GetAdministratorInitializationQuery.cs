using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Queries;

public sealed record GetAdministratorInitializationQuery : IQuery<bool>;

public sealed class GetAdministratorInitializationQueryHandler(
    ContentServerDbContext db
) : IQueryHandler<GetAdministratorInitializationQuery, bool>
{
    public async Task<bool> Handle(
        GetAdministratorInitializationQuery request,
        CancellationToken cancellationToken)
    {
        return !await db.Administrators.AsNoTracking().AnyAsync(cancellationToken);
    }
}
