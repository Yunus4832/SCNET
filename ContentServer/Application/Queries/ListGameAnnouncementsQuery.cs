using ContentServer.Domain.Announcements;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Queries;

public sealed record ListGameAnnouncementsQuery(bool PublicOnly) : IQuery<IReadOnlyList<GameAnnouncement>>;

public sealed class ListGameAnnouncementsQueryHandler(ContentServerDbContext db)
    : IQueryHandler<ListGameAnnouncementsQuery, IReadOnlyList<GameAnnouncement>>
{
    public async Task<IReadOnlyList<GameAnnouncement>> Handle(ListGameAnnouncementsQuery request,
        CancellationToken cancellationToken)
    {
        var query = db.GameAnnouncements.AsNoTracking();
        if (request.PublicOnly)
        {
            query = query.Where(item => item.Status == AnnouncementStatus.Published);
        }

        var items = await query.ToArrayAsync(cancellationToken);
        return request.PublicOnly
            ? items.OrderByDescending(item => item.PublishedAt).Take(100).ToArray()
            : items.OrderByDescending(item => item.CreatedAt).ToArray();
    }
}
