using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Queries;

public sealed record GetPublisherQuery(PublisherId PublisherId) : IQuery<PublisherDto?>;

public sealed class GetPublisherQueryHandler(
    ContentServerDbContext db
) : IQueryHandler<GetPublisherQuery, PublisherDto?>
{
    public async Task<PublisherDto?> Handle(
        GetPublisherQuery query,
        CancellationToken cancellationToken)
    {
        return await db.Publishers
            .AsNoTracking()
            .Where(publisher => publisher.Id == query.PublisherId)
            .Select(publisher => new PublisherDto(
                publisher.Id,
                publisher.DisplayName,
                publisher.Contact,
                publisher.Description,
                publisher.Status,
                publisher.Keys.Any(key => key.RevokedAt == null),
                publisher.ReviewMessage,
                publisher.CreatedAt,
                publisher.ReviewedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
