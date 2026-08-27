using ContentServer.Domain.Contents;
using ContentServer.Domain.Publishers;
using ContentServer.Extensions;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Queries;

public sealed record ContentItemDto(
    ContentId ContentId,
    PublisherId PublisherId,
    string Type,
    string Identifier,
    string Name,
    string? Summary,
    ContentStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ListContentItemsQuery(
    PageRequest Page,
    string? Search = null,
    PublisherId? PublisherId = null,
    string? Type = null)
    : IQuery<PagedData<ContentItemDto>>;

public sealed class ListContentItemsQueryHandler(ContentServerDbContext db)
    : IQueryHandler<ListContentItemsQuery, PagedData<ContentItemDto>>
{
    public async Task<PagedData<ContentItemDto>> Handle(ListContentItemsQuery query, CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        return await db.Contents.AsNoTracking()
            .WhereIf(query.PublisherId is not null, content => content.PublisherId == query.PublisherId)
            .WhereIf(!string.IsNullOrWhiteSpace(query.Type), content => content.Type == query.Type)
            .WhereIf(search is not null, content =>
                content.Name.Contains(search!) ||
                content.Identifier.Contains(search!) ||
                content.Summary != null &&
                content.Summary.Contains(search!))
            .OrderByDescending(content => content.Id)
            .Select(content => new ContentItemDto(
                content.Id,
                content.PublisherId,
                content.Type,
                content.Identifier,
                content.Name,
                content.Summary,
                content.Status,
                content.CreatedAt,
                content.UpdatedAt))
            .ToPagedDataAsync(query.Page, cancellationToken);
    }
}
