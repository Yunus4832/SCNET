using ContentServer.Domain.Publishers;
using ContentServer.Domain.ServerDirectory;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record SubmitDirectoryServerCommand(PublisherId PublisherId, string Name, string Address,
    string? Description, IReadOnlyList<string> Tags) : ICommand<DirectoryServerId>;

public sealed class SubmitDirectoryServerCommandHandler(DirectoryServerRepository repository)
    : ICommandHandler<SubmitDirectoryServerCommand, DirectoryServerId>
{
    public async Task<DirectoryServerId> Handle(SubmitDirectoryServerCommand command,
        CancellationToken cancellationToken)
    {
        if (await repository.HasPendingOrApprovedAddressAsync(command.Address, cancellationToken))
        {
            throw new InvalidOperationException("Server address is already registered.");
        }

        var server = DirectoryServer.Submit(command.PublisherId, command.Name, command.Address,
            command.Description, command.Tags, DateTimeOffset.UtcNow);
        await repository.AddAsync(server, cancellationToken);
        return server.Id;
    }
}
