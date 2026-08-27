using ContentServer.Domain.Administration;
using ContentServer.Infrastructure;
using ContentServer.Utils;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record ApplyAdministratorCommand(string Name, string Contact, string? Description)
    : ICommand<AdministratorApplicationDto>;
public sealed record AdministratorApplicationDto(AdministratorId AdministratorId, AdministratorStatus Status, string ApiKey);
public sealed class ApplyAdministratorCommandHandler(AdministratorRepository repository)
    : ICommandHandler<ApplyAdministratorCommand, AdministratorApplicationDto>
{
    public async Task<AdministratorApplicationDto> Handle(ApplyAdministratorCommand command, CancellationToken cancellationToken)
    {
        var key = ApiKeyUtility.GenerateAdministratorKey();
        var item = Administrator.Apply(command.Name, command.Contact, command.Description,
            ApiKeyUtility.GetPrefix(key), ApiKeyUtility.Hash(key), DateTimeOffset.UtcNow);
        await repository.AddAsync(item, cancellationToken);
        return new(item.Id, item.Status, key);
    }
}
