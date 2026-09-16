using ContentServer.Domain.ServerDirectory;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record DeleteDirectoryServerCommand(DirectoryServerId Id) : ICommand<bool>;

public sealed class DeleteDirectoryServerCommandHandler(DirectoryServerRepository repository)
    : ICommandHandler<DeleteDirectoryServerCommand, bool>
{
    public async Task<bool> Handle(DeleteDirectoryServerCommand command, CancellationToken cancellationToken)
    {
        var server = await repository.FindAsync(command.Id, cancellationToken);
        if (server is null)
        {
            return false;
        }

        repository.Delete(server);
        return true;
    }
}
