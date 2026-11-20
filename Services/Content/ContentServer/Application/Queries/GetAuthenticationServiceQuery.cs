using ContentServer.Domain.AuthenticationServices;
using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Queries;

public sealed record GetAuthenticationServiceQuery(
    AuthenticationServiceRegistrationId Id,
    PublisherId? PublisherId = null
) : IQuery<AuthenticationServiceDto?>;

public sealed class GetAuthenticationServiceQueryHandler(ContentServerDbContext db)
    : IQueryHandler<GetAuthenticationServiceQuery, AuthenticationServiceDto?>
{
    public Task<AuthenticationServiceDto?> Handle(
        GetAuthenticationServiceQuery query,
        CancellationToken cancellationToken
    )
    {
        var registrations = db.AuthenticationServices.AsNoTracking().Where(registration => registration.Id == query.Id);
        if (query.PublisherId is { } publisherId)
        {
            registrations = registrations.Where(registration => registration.PublisherId == publisherId);
        }

        return AuthenticationServiceProjection.Project(registrations, db.Publishers.AsNoTracking())
            .SingleOrDefaultAsync(cancellationToken);
    }
}
