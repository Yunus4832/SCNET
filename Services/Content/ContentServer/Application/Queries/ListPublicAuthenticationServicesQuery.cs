using ContentServer.Domain.AuthenticationServices;
using ContentServer.Domain.Publishers;
using ContentServer.Extensions;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Queries;

public sealed record ListPublicAuthenticationServicesQuery(PageRequest Page)
    : IQuery<PagedData<AuthenticationServiceDto>>;

public sealed class ListPublicAuthenticationServicesQueryHandler(ContentServerDbContext db)
    : IQueryHandler<ListPublicAuthenticationServicesQuery, PagedData<AuthenticationServiceDto>>
{
    public Task<PagedData<AuthenticationServiceDto>> Handle(
        ListPublicAuthenticationServicesQuery query,
        CancellationToken cancellationToken
    )
    {
        return AuthenticationServiceProjection.Project(
                db.AuthenticationServices.AsNoTracking()
                    .Where(registration => registration.Status == AuthenticationServiceRegistrationStatus.Active)
                    .OrderByDescending(registration => registration.Id),
                db.Publishers.AsNoTracking().Where(publisher => publisher.Status == PublisherStatus.Active)
            )
            .ToPagedDataAsync(query.Page, cancellationToken);
    }
}
