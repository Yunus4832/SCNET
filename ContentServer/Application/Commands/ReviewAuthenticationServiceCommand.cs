using ContentServer.Domain.Administration;
using ContentServer.Domain.AuthenticationServices;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record ReviewAuthenticationServiceCommand(
    AuthenticationServiceRegistrationId Id,
    long Revision,
    AdministratorId AdministratorId,
    bool Approved,
    string? Message,
    DateTimeOffset Now
)
    : ICommand<AuthenticationServiceMutationResult>;

public sealed class ReviewAuthenticationServiceCommandHandler(AuthenticationServiceRegistrationRepository repository)
    : ICommandHandler<ReviewAuthenticationServiceCommand, AuthenticationServiceMutationResult>
{
    public async Task<AuthenticationServiceMutationResult> Handle(
        ReviewAuthenticationServiceCommand command,
        CancellationToken cancellationToken
    )
    {
        var registration = await repository.FindAsync(command.Id, cancellationToken);
        if (registration is null) { return AuthenticationServiceMutationResult.NotFound; }

        return registration.Review(
            command.Approved,
            command.AdministratorId,
            command.Message,
            command.Revision,
            command.Now
        )
            ? AuthenticationServiceMutationResult.Completed
            : AuthenticationServiceMutationResult.Conflict;
    }
}
