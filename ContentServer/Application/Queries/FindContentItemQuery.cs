using ContentServer.Domain.Contents;
using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Queries;

public sealed record ContentItemLookupDto(ContentId ContentId, PublisherId PublisherId, string Type);

public sealed record FindContentItemQuery(
    PublisherId PublisherId,
    string Identifier
) : IQuery<ContentItemLookupDto?>;

public sealed class FindContentItemQueryHandler(
    ContentServerDbContext db
) : IQueryHandler<FindContentItemQuery, ContentItemLookupDto?>
{
    public async Task<ContentItemLookupDto?> Handle(
        FindContentItemQuery query,
        CancellationToken cancellationToken)
    {
        var normalizedIdentifier = query.Identifier.ToLowerInvariant();
        return await db.Contents.AsNoTracking()
            .Where(content => content.NormalizedIdentifier == normalizedIdentifier)
            .Select(content => new ContentItemLookupDto(content.Id, content.PublisherId, content.Type))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
