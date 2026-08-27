using ContentServer.Domain.Administration;
using ContentServer.Infrastructure;
using ContentServer.Utils;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record InitializeAdministratorCommand(
    string Name,
    string ApiKey) : ICommand<AdministratorInitializationDto?>;

public sealed record AdministratorInitializationDto(
    AdministratorId AdministratorId,
    string Name,
    AdministratorStatus Status);

public sealed class InitializeAdministratorCommandHandler(
    AdministratorRepository repository
) : ICommandHandler<InitializeAdministratorCommand, AdministratorInitializationDto?>
{
    public async Task<AdministratorInitializationDto?> Handle(
        InitializeAdministratorCommand command,
        CancellationToken cancellationToken)
    {
        if (await repository.AnyAsync(cancellationToken))
        {
            return null;
        }

        var administrator = Administrator.Create(
            command.Name,
            ApiKeyUtility.GetPrefix(command.ApiKey),
            ApiKeyUtility.Hash(command.ApiKey),
            DateTimeOffset.UtcNow,
            isSuperAdministrator: true
        );
        await repository.AddAsync(administrator, cancellationToken);
        return new AdministratorInitializationDto(
            administrator.Id,
            administrator.Name,
            administrator.Status
        );
    }
}
