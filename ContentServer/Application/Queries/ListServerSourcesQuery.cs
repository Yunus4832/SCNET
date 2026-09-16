using ContentServer.Domain.Publishers;
using ContentServer.Domain.ServerSources;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Queries;

public sealed record ServerSourceDto(ServerSourceRegistrationId Id, PublisherId PublisherId, string Name, string ApiUrl,
    string? Description, ServerSourceRegistrationStatus Status, string? ReviewMessage, DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt);

public sealed record ListServerSourcesQuery(ServerSourceRegistrationStatus? Status = null, bool PublicOnly = false,
    ServerSourceRegistrationId? Id = null, PublisherId? PublisherId = null)
    : IQuery<IReadOnlyList<ServerSourceDto>>;

public sealed class ListServerSourcesQueryHandler(ContentServerDbContext db)
    : IQueryHandler<ListServerSourcesQuery, IReadOnlyList<ServerSourceDto>>
{
    public async Task<IReadOnlyList<ServerSourceDto>> Handle(ListServerSourcesQuery query,
        CancellationToken cancellationToken)
    {
        var sources = db.ServerSources.AsNoTracking();
        if (query.Id is not null)
        {
            sources = sources.Where(source => source.Id == query.Id);
        }

        if (query.PublisherId is not null)
        {
            sources = sources.Where(source => source.PublisherId == query.PublisherId);
        }

        if (query.PublicOnly)
        {
            sources = sources.Where(source => source.Status == ServerSourceRegistrationStatus.Active);
        }
        else if (query.Status is not null)
        {
            sources = sources.Where(source => source.Status == query.Status);
        }

        return await sources.OrderByDescending(source => source.Id)
            .Select(source => new ServerSourceDto(source.Id, source.PublisherId, source.Name, source.ApiUrl,
                source.Description, source.Status, source.ReviewMessage, source.CreatedAt, source.ReviewedAt))
            .ToArrayAsync(cancellationToken);
    }
}
