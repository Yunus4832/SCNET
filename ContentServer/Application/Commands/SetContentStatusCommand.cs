using ContentServer.Domain.Administration;
using ContentServer.Domain.Contents;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record SetContentStatusCommand(
    ContentId ContentId,
    AdministratorId AdministratorId,
    ContentStatus Status
) : ICommand<bool>;

public sealed class SetContentStatusCommandHandler(
    ContentRepository repository
) : ICommandHandler<SetContentStatusCommand, bool>
{
    public async Task<bool> Handle(SetContentStatusCommand command, CancellationToken cancellationToken)
    {
        var content = await repository.FindAsync(command.ContentId, cancellationToken);
        if (content is null)
        {
            return false;
        }

        content.SetStatus(
            command.Status,
            command.AdministratorId,
            DateTimeOffset.UtcNow
        );
        return true;
    }
}
