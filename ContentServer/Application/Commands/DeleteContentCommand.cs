using ContentServer.Domain.Contents;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record DeleteContentCommand(ContentId ContentId) : ICommand<bool>;

public sealed class DeleteContentCommandHandler(ContentRepository repository)
    : ICommandHandler<DeleteContentCommand, bool>
{
    public async Task<bool> Handle(DeleteContentCommand command,
        CancellationToken cancellationToken)
    {
        var content = await repository.FindWithVersionsAsync(command.ContentId, cancellationToken);
        if (content is null)
        {
            return false;
        }

        content.Delete(DateTimeOffset.UtcNow);
        return true;
    }
}
