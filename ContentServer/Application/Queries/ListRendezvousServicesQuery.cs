using ContentServer.Domain.Publishers;
using ContentServer.Domain.RendezvousServices;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Queries;

public sealed record RendezvousServiceDto(RendezvousServiceRegistrationId Id, PublisherId PublisherId, string PublisherName,
    string Name, string ControlUrl, string? Description, RendezvousServiceRegistrationStatus Status, string? ReviewMessage,
    DateTimeOffset CreatedAt, DateTimeOffset? ReviewedAt);

public sealed record ListRendezvousServicesQuery(RendezvousServiceRegistrationStatus? Status = null,
    RendezvousServiceRegistrationId? Id = null, PublisherId? PublisherId = null)
    : IQuery<IReadOnlyList<RendezvousServiceDto>>;

public sealed class ListRendezvousServicesQueryHandler(ContentServerDbContext db)
    : IQueryHandler<ListRendezvousServicesQuery, IReadOnlyList<RendezvousServiceDto>>
{
    public async Task<IReadOnlyList<RendezvousServiceDto>> Handle(ListRendezvousServicesQuery query,
        CancellationToken cancellationToken)
    {
        var sources = db.RendezvousServices.AsNoTracking();
        if (query.Id is not null)
        {
            sources = sources.Where(source => source.Id == query.Id);
        }

        if (query.PublisherId is not null)
        {
            sources = sources.Where(source => source.PublisherId == query.PublisherId);
        }

        if (query.Status is not null)
        {
            sources = sources.Where(source => source.Status == query.Status);
        }

        return await sources.OrderByDescending(source => source.Id)
            .Join(db.Publishers.AsNoTracking(), source => source.PublisherId, publisher => publisher.Id,
                (source, publisher) => new RendezvousServiceDto(source.Id, source.PublisherId, publisher.DisplayName,
                    source.Name, source.ControlUrl, source.Description, source.Status, source.ReviewMessage,
                    source.CreatedAt, source.ReviewedAt))
            .ToArrayAsync(cancellationToken);
    }
}
