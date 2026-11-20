using ContentServer.Domain.AuthenticationServices;
using ContentServer.Domain.Publishers;
using ContentServer.Extensions;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Queries;

public sealed record ListAuthenticationServicesQuery(
    PageRequest Page,
    PublisherId? PublisherId = null,
    AuthenticationServiceRegistrationStatus? Status = null
) : IQuery<PagedData<AuthenticationServiceDto>>;

public sealed class ListAuthenticationServicesQueryHandler(ContentServerDbContext db)
    : IQueryHandler<ListAuthenticationServicesQuery, PagedData<AuthenticationServiceDto>>
{
    public Task<PagedData<AuthenticationServiceDto>> Handle(
        ListAuthenticationServicesQuery query,
        CancellationToken cancellationToken
    )
    {
        var registrations = db.AuthenticationServices.AsNoTracking();
        if (query.PublisherId is { } publisherId)
        {
            registrations = registrations.Where(registration => registration.PublisherId == publisherId);
        }

        if (query.Status is { } status)
        {
            registrations = registrations.Where(registration => registration.Status == status);
        }

        return AuthenticationServiceProjection.Project(
            registrations.OrderByDescending(registration => registration.Id),
            db.Publishers.AsNoTracking()
        ).ToPagedDataAsync(query.Page, cancellationToken);
    }
}
