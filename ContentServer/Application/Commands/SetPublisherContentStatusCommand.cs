using ContentServer.Domain.Contents;
using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public enum SetPublisherContentStatusResult
{
    Completed,
    NotFound,
    NotOwned
}

public sealed record SetPublisherContentStatusCommand(
    ContentId ContentId,
    PublisherId PublisherId,
    ContentStatus Status
) : ICommand<SetPublisherContentStatusResult>;

public sealed class SetPublisherContentStatusCommandHandler(
    ContentRepository repository
) : ICommandHandler<SetPublisherContentStatusCommand, SetPublisherContentStatusResult>
{
    public async Task<SetPublisherContentStatusResult> Handle(
        SetPublisherContentStatusCommand command,
        CancellationToken cancellationToken)
    {
        var content = await repository.FindAsync(command.ContentId, cancellationToken);
        if (content is null)
        {
            return SetPublisherContentStatusResult.NotFound;
        }

        if (content.PublisherId != command.PublisherId)
        {
            return SetPublisherContentStatusResult.NotOwned;
        }

        content.SetStatus(command.Status, DateTimeOffset.UtcNow);
        return SetPublisherContentStatusResult.Completed;
    }
}
