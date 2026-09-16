using ContentServer.Domain.Publishers;
using ContentServer.Domain.ServerDirectory;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public enum UpdateDirectoryServerResult
{
    Success,
    NotFound,
    NotOwned,
    AddressConflict
}

public sealed record UpdateDirectoryServerCommand(DirectoryServerId Id, PublisherId? PublisherId, string Name,
    string Address, string? Description, IReadOnlyList<string> Tags) : ICommand<UpdateDirectoryServerResult>;

public sealed class UpdateDirectoryServerCommandHandler(DirectoryServerRepository repository)
    : ICommandHandler<UpdateDirectoryServerCommand, UpdateDirectoryServerResult>
{
    public async Task<UpdateDirectoryServerResult> Handle(UpdateDirectoryServerCommand command,
        CancellationToken cancellationToken)
    {
        var server = await repository.FindAsync(command.Id, cancellationToken);
        if (server is null)
        {
            return UpdateDirectoryServerResult.NotFound;
        }

        if (command.PublisherId is not null && server.PublisherId != command.PublisherId)
        {
            return UpdateDirectoryServerResult.NotOwned;
        }

        if (await repository.HasPendingOrApprovedAddressAsync(command.Address, command.Id, cancellationToken))
        {
            return UpdateDirectoryServerResult.AddressConflict;
        }

        server.Update(command.Name, command.Address, command.Description, command.Tags,
            requiresReview: command.PublisherId is not null, now: DateTimeOffset.UtcNow);
        return UpdateDirectoryServerResult.Success;
    }
}
