using ContentServer.Domain.AuthenticationServices;
using ContentServer.Extensions;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Queries;

public sealed record AuthenticationServiceAuditDto(
    long Revision,
    string PublisherId,
    string Action,
    string? AdministratorId,
    string? Message,
    DateTimeOffset OccurredAt,
    string Name,
    string ServiceUrl,
    string? Description,
    AuthenticationServiceRegistrationStatus Status
);

public sealed record ListAuthenticationServiceAuditQuery(AuthenticationServiceRegistrationId Id, PageRequest Page)
    : IQuery<PagedData<AuthenticationServiceAuditDto>>;

public sealed class ListAuthenticationServiceAuditQueryHandler(ContentServerDbContext db)
    : IQueryHandler<ListAuthenticationServiceAuditQuery, PagedData<AuthenticationServiceAuditDto>>
{
    public Task<PagedData<AuthenticationServiceAuditDto>> Handle(
        ListAuthenticationServiceAuditQuery query,
        CancellationToken cancellationToken
    )
    {
        return db.AuthenticationServices.AsNoTracking().IncludeDeleted()
            .Where(registration => registration.Id == query.Id)
            .SelectMany(
                registration => registration.Audit,
                (registration, entry) => new { registration.PublisherId, Entry = entry }
            )
            .OrderByDescending(item => item.Entry.Revision)
            .Select(item => new AuthenticationServiceAuditDto(
                    item.Entry.Revision,
                    item.PublisherId.ToString(),
                    item.Entry.Action,
                    item.Entry.AdministratorId == null ? null : item.Entry.AdministratorId.ToString(),
                    item.Entry.Message,
                    item.Entry.OccurredAt,
                    item.Entry.Name,
                    item.Entry.ServiceUrl,
                    item.Entry.Description,
                    item.Entry.Status
                )
            )
            .ToPagedDataAsync(query.Page, cancellationToken);
    }
}
