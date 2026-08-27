using ContentServer.Domain.Administration;
using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record SuspendPublisherCommand(
    PublisherId PublisherId,
    AdministratorId AdministratorId,
    string? Message) : ICommand<ReviewPublisherResult>;

public sealed class SuspendPublisherCommandHandler(PublisherRepository repository)
    : ICommandHandler<SuspendPublisherCommand, ReviewPublisherResult>
{
    public async Task<ReviewPublisherResult> Handle(
        SuspendPublisherCommand command,
        CancellationToken cancellationToken)
    {
        var publisher = await repository.FindAsync(command.PublisherId, cancellationToken);
        if (publisher is null)
        {
            return ReviewPublisherResult.NotFound;
        }

        return publisher.Suspend(command.AdministratorId, command.Message, DateTimeOffset.UtcNow)
            ? ReviewPublisherResult.Completed
            : ReviewPublisherResult.InvalidState;
    }
}
