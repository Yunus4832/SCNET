using ContentServer.Domain.Administration;
using ContentServer.Domain.Publishers;
using ContentServer.Domain.ServerDirectory;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public enum SetDirectoryServerStateResult
{
    Success,
    NotFound,
    NotOwned,
    InvalidState
}

public sealed record SetPublisherDirectoryServerEnabledCommand(DirectoryServerId Id, PublisherId PublisherId,
    bool Enabled) : ICommand<SetDirectoryServerStateResult>;

public sealed class SetPublisherDirectoryServerEnabledCommandHandler(DirectoryServerRepository repository)
    : ICommandHandler<SetPublisherDirectoryServerEnabledCommand, SetDirectoryServerStateResult>
{
    public async Task<SetDirectoryServerStateResult> Handle(SetPublisherDirectoryServerEnabledCommand command,
        CancellationToken cancellationToken)
    {
        var server = await repository.FindAsync(command.Id, cancellationToken);
        if (server is null)
        {
            return SetDirectoryServerStateResult.NotFound;
        }

        if (server.PublisherId != command.PublisherId)
        {
            return SetDirectoryServerStateResult.NotOwned;
        }

        server.SetPublisherEnabled(command.Enabled, DateTimeOffset.UtcNow);
        return SetDirectoryServerStateResult.Success;
    }
}

public sealed record SetDirectoryServerSuspendedCommand(DirectoryServerId Id, AdministratorId AdministratorId,
    bool Suspended, string? Reason) : ICommand<SetDirectoryServerStateResult>;

public sealed class SetDirectoryServerSuspendedCommandHandler(DirectoryServerRepository repository)
    : ICommandHandler<SetDirectoryServerSuspendedCommand, SetDirectoryServerStateResult>
{
    public async Task<SetDirectoryServerStateResult> Handle(SetDirectoryServerSuspendedCommand command,
        CancellationToken cancellationToken)
    {
        var server = await repository.FindAsync(command.Id, cancellationToken);
        if (server is null)
        {
            return SetDirectoryServerStateResult.NotFound;
        }

        var changed = command.Suspended
            ? server.Suspend(command.AdministratorId, command.Reason, DateTimeOffset.UtcNow)
            : server.Restore(command.AdministratorId, DateTimeOffset.UtcNow);
        return changed ? SetDirectoryServerStateResult.Success : SetDirectoryServerStateResult.InvalidState;
    }
}
