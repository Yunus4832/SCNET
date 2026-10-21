using ContentServer.Domain.RendezvousServices;
using ContentServer.Extensions;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Queries;

public sealed record ListPublicRendezvousServicesQuery(PageRequest Page) : IQuery<PagedData<RendezvousServiceDto>>;

public sealed class ListPublicRendezvousServicesQueryHandler(ContentServerDbContext db)
    : IQueryHandler<ListPublicRendezvousServicesQuery, PagedData<RendezvousServiceDto>>
{
    public Task<PagedData<RendezvousServiceDto>> Handle(
        ListPublicRendezvousServicesQuery query,
        CancellationToken cancellationToken
    )
    {
        return db.RendezvousServices.AsNoTracking()
            .Where(service => service.Status == RendezvousServiceRegistrationStatus.Active)
            .OrderByDescending(service => service.Id)
            .Join(
                db.Publishers.AsNoTracking(),
                service => service.PublisherId,
                publisher => publisher.Id,
                (service, publisher) => new RendezvousServiceDto(
                    service.Id,
                    service.PublisherId,
                    publisher.DisplayName,
                    service.Name,
                    service.ControlUrl,
                    service.Description,
                    service.Status,
                    service.ReviewMessage,
                    service.CreatedAt,
                    service.ReviewedAt
                )
            )
            .ToPagedDataAsync(query.Page, cancellationToken);
    }
}
