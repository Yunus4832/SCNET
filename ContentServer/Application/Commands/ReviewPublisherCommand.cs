using ContentServer.Domain.Administration;
using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record ReviewPublisherCommand(
    PublisherId PublisherId,
    AdministratorId AdministratorId,
    PublisherStatus Status,
    string? Message
) : ICommand<ReviewPublisherResult>;

public enum ReviewPublisherResult
{
    Completed,
    NotFound,
    InvalidState
}

public sealed class ReviewPublisherCommandHandler(
    PublisherRepository repository
) : ICommandHandler<ReviewPublisherCommand, ReviewPublisherResult>
{
    public async Task<ReviewPublisherResult> Handle(ReviewPublisherCommand command, CancellationToken cancellationToken)
    {
        var publisher = await repository.FindAsync(command.PublisherId, cancellationToken);
        if (publisher is null)
        {
            return ReviewPublisherResult.NotFound;
        }

        return publisher.Review(
            command.Status,
            command.AdministratorId,
            command.Message,
            DateTimeOffset.UtcNow
        )
            ? ReviewPublisherResult.Completed
            : ReviewPublisherResult.InvalidState;
    }
}
