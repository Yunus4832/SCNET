using ContentServer.Domain.Administration;
using ContentServer.Extensions;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Queries;

public sealed record AdministratorDto(
    AdministratorId AdministratorId,
    string Name,
    string Contact,
    string? Description,
    AdministratorStatus Status,
    bool IsSuperAdministrator,
    bool HasActiveKey,
    string? ReviewMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt
);

public sealed record ListAdministratorsQuery(
    PageRequest Page,
    AdministratorStatus? Status = null,
    AdministratorId? AdministratorId = null,
    string? Search = null
) : IQuery<PagedData<AdministratorDto>>;

public sealed class ListAdministratorsQueryHandler(
    ContentServerDbContext db
) : IQueryHandler<ListAdministratorsQuery, PagedData<AdministratorDto>>
{
    public async Task<PagedData<AdministratorDto>> Handle(ListAdministratorsQuery q, CancellationToken ct)
    {
        var search = string.IsNullOrWhiteSpace(q.Search) ? null : q.Search.Trim();
        return await db.Administrators.AsNoTracking()
            .WhereIf(q.Status is not null, x => x.Status == q.Status)
            .WhereIf(q.AdministratorId is not null, x => x.Id == q.AdministratorId)
            .WhereIf(search is not null, x => x.Name.Contains(search!) || x.Contact.Contains(search!))
            .OrderByDescending(x => x.Id)
            .Select(x => new AdministratorDto(
                x.Id,
                x.Name,
                x.Contact,
                x.Description,
                x.Status,
                x.IsSuperAdministrator,
                x.Keys.Any(key => key.RevokedAt == null),
                x.ReviewMessage,
                x.CreatedAt,
                x.ReviewedAt
            ))
            .ToPagedDataAsync(q.Page, ct);
    }
}
