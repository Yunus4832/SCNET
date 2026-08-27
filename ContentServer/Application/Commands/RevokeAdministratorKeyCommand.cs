using ContentServer.Domain.Administration;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record RevokeAdministratorKeyCommand(
    AdministratorId AdministratorId
) : ICommand<bool>;

public sealed class RevokeAdministratorKeyCommandHandler(
    AdministratorRepository repository
) : ICommandHandler<RevokeAdministratorKeyCommand, bool>
{
    public async Task<bool> Handle(RevokeAdministratorKeyCommand command, CancellationToken cancellationToken)
    {
        var administrator = await repository.FindWithKeysAsync(command.AdministratorId, cancellationToken);
        return administrator is not null && administrator.RevokeKeys(DateTimeOffset.UtcNow);
    }
}

public sealed record RestoreAdministratorKeyCommand(
    AdministratorId AdministratorId
) : ICommand<bool>;

public sealed class RestoreAdministratorKeyCommandHandler(
    AdministratorRepository repository
) : ICommandHandler<RestoreAdministratorKeyCommand, bool>
{
    public async Task<bool> Handle(RestoreAdministratorKeyCommand command, CancellationToken cancellationToken)
    {
        var administrator = await repository.FindWithKeysAsync(command.AdministratorId, cancellationToken);
        return administrator is not null && administrator.RestoreKeys(DateTimeOffset.UtcNow);
    }
}
