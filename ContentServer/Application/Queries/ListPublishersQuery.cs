using ContentServer.Domain.Publishers;
using ContentServer.Extensions;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Queries;

public sealed record PublisherDto(
    PublisherId PublisherId,
    string DisplayName,
    string Contact,
    string? Description,
    PublisherStatus Status,
    bool HasActiveKey,
    string? ReviewMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt);

public sealed record ListPublishersQuery(
    PageRequest Page,
    PublisherStatus? Status = null,
    string? Search = null) : IQuery<PagedData<PublisherDto>>;

public sealed class ListPublishersQueryHandler(
    ContentServerDbContext db
) : IQueryHandler<ListPublishersQuery, PagedData<PublisherDto>>
{
    public async Task<PagedData<PublisherDto>> Handle(
        ListPublishersQuery query,
        CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        return await db.Publishers.AsNoTracking()
            .WhereIf(
                query.Status is not null,
                publisher => publisher.Status == query.Status)
            .WhereIf(search is not null,
                publisher => publisher.DisplayName.Contains(search!) ||
                             publisher.Contact.Contains(search!))
            .OrderByDescending(publisher => publisher.Id)
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
            .ToPagedDataAsync(query.Page, cancellationToken);
    }
}
