using ContentServer.Domain.Administration;
using ContentServer.Domain.AuthenticationServices;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record DisableAuthenticationServiceCommand(
    AuthenticationServiceRegistrationId Id,
    long Revision,
    AdministratorId AdministratorId,
    string? Reason,
    DateTimeOffset Now
) : ICommand<AuthenticationServiceMutationResult>;

public sealed class DisableAuthenticationServiceCommandHandler(AuthenticationServiceRegistrationRepository repository)
    : ICommandHandler<DisableAuthenticationServiceCommand, AuthenticationServiceMutationResult>
{
    public async Task<AuthenticationServiceMutationResult> Handle(
        DisableAuthenticationServiceCommand command,
        CancellationToken cancellationToken
    )
    {
        var registration = await repository.FindAsync(command.Id, cancellationToken);
        if (registration is null) { return AuthenticationServiceMutationResult.NotFound; }

        return registration.Disable(command.AdministratorId, command.Reason, command.Revision, command.Now)
            ? AuthenticationServiceMutationResult.Completed
            : AuthenticationServiceMutationResult.Conflict;
    }
}
