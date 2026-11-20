using ContentServer.Domain.AuthenticationServices;
using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record WithdrawAuthenticationServiceCommand(
    AuthenticationServiceRegistrationId Id,
    long Revision,
    PublisherId PublisherId,
    DateTimeOffset Now
) : ICommand<AuthenticationServiceMutationResult>;

public sealed class WithdrawAuthenticationServiceCommandHandler(AuthenticationServiceRegistrationRepository repository)
    : ICommandHandler<WithdrawAuthenticationServiceCommand, AuthenticationServiceMutationResult>
{
    public async Task<AuthenticationServiceMutationResult> Handle(
        WithdrawAuthenticationServiceCommand command,
        CancellationToken cancellationToken
    )
    {
        var registration = await repository.FindAsync(command.Id, cancellationToken);
        if (registration is null || registration.PublisherId != command.PublisherId)
        {
            return AuthenticationServiceMutationResult.NotFound;
        }

        return registration.Withdraw(command.Revision, command.Now)
            ? AuthenticationServiceMutationResult.Completed
            : AuthenticationServiceMutationResult.Conflict;
    }
}
