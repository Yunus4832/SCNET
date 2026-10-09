using ContentServer.Infrastructure;

using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record DeleteContentReviewsCommand(string ContentId, IReadOnlyCollection<string> VersionIds) : ICommand;

public sealed class DeleteContentReviewsCommandHandler(ContentServerDbContext db)
    : ICommandHandler<DeleteContentReviewsCommand>
{
    public async Task Handle(DeleteContentReviewsCommand command, CancellationToken cancellationToken)
    {
        var reviews = await db.ReviewRecords.Where(review =>
                (review.TargetType == "Content" && review.TargetId == command.ContentId) ||
                (review.TargetType == "ContentVersion" && command.VersionIds.Contains(review.TargetId)))
            .ToListAsync(cancellationToken);
        foreach (var review in reviews)
        {
            review.Delete();
        }
    }
}
