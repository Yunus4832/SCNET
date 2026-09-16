using ContentServer.Domain.Publishers;
using ContentServer.Domain.ServerDirectory;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Queries;

public sealed record DirectoryServerDto(DirectoryServerId Id, PublisherId PublisherId, string Name, string Address,
    string? Description, string TagsJson, DirectoryServerReviewStatus ReviewStatus, string? ReviewMessage,
    bool IsEnabledByPublisher, DateTimeOffset? SuspendedAt, string? SuspensionReason, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, DateTimeOffset? ReviewedAt);

public sealed record ListDirectoryServersQuery(PublisherId? PublisherId = null,
    DirectoryServerReviewStatus? ReviewStatus = null, bool PublicOnly = false, DirectoryServerId? Id = null,
    int? Offset = null, int? Limit = null) : IQuery<IReadOnlyList<DirectoryServerDto>>;

public sealed class ListDirectoryServersQueryHandler(ContentServerDbContext db)
    : IQueryHandler<ListDirectoryServersQuery, IReadOnlyList<DirectoryServerDto>>
{
    public async Task<IReadOnlyList<DirectoryServerDto>> Handle(ListDirectoryServersQuery query,
        CancellationToken cancellationToken)
    {
        var servers = db.DirectoryServers.AsNoTracking();
        if (query.PublisherId is not null)
        {
            servers = servers.Where(server => server.PublisherId == query.PublisherId);
        }

        if (query.ReviewStatus is not null)
        {
            servers = servers.Where(server => server.ReviewStatus == query.ReviewStatus);
        }

        if (query.Id is not null)
        {
            servers = servers.Where(server => server.Id == query.Id);
        }

        if (query.PublicOnly)
        {
            servers = servers.Where(server => server.ReviewStatus == DirectoryServerReviewStatus.Approved &&
                                              server.IsEnabledByPublisher && server.SuspendedAt == null);
        }

        var ordered = servers.OrderBy(server => server.Id);
        if (query.Offset is not null)
        {
            ordered = (IOrderedQueryable<DirectoryServer>)ordered.Skip(query.Offset.Value);
        }

        IQueryable<DirectoryServer> selected = ordered;
        if (query.Limit is not null)
        {
            selected = selected.Take(query.Limit.Value);
        }

        return await selected.Select(server => new DirectoryServerDto(server.Id, server.PublisherId, server.Name,
            server.Address, server.Description, server.TagsJson, server.ReviewStatus, server.ReviewMessage,
            server.IsEnabledByPublisher, server.SuspendedAt, server.SuspensionReason, server.CreatedAt,
            server.UpdatedAt, server.ReviewedAt)).ToArrayAsync(cancellationToken);
    }
}
