using ContentServer.Domain.Administration;
using ContentServer.Domain.AuthenticationServices;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record DeleteAdminAuthenticationServiceCommand(
    AuthenticationServiceRegistrationId Id,
    long Revision,
    AdministratorId AdministratorId,
    DateTimeOffset Now
) : ICommand<AuthenticationServiceMutationResult>;

public sealed class DeleteAdminAuthenticationServiceCommandHandler(
    AuthenticationServiceRegistrationRepository repository
) : ICommandHandler<DeleteAdminAuthenticationServiceCommand, AuthenticationServiceMutationResult>
{
    public async Task<AuthenticationServiceMutationResult> Handle(
        DeleteAdminAuthenticationServiceCommand command,
        CancellationToken cancellationToken
    )
    {
        var registration = await repository.FindAsync(command.Id, cancellationToken);
        if (registration is null) { return AuthenticationServiceMutationResult.NotFound; }

        return registration.Delete(command.AdministratorId, command.Revision, command.Now)
            ? AuthenticationServiceMutationResult.Completed
            : AuthenticationServiceMutationResult.Conflict;
    }
}
