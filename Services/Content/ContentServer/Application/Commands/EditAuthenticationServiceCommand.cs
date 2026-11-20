using ContentServer.Domain.AuthenticationServices;
using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record EditAuthenticationServiceCommand(
    AuthenticationServiceRegistrationId Id,
    PublisherId PublisherId,
    long Revision,
    string Name,
    Uri ServiceUrl,
    string? Description,
    DateTimeOffset Now
)
    : ICommand<AuthenticationServiceMutationResult>;

public sealed class EditAuthenticationServiceCommandHandler(AuthenticationServiceRegistrationRepository repository)
    : ICommandHandler<EditAuthenticationServiceCommand, AuthenticationServiceMutationResult>
{
    public async Task<AuthenticationServiceMutationResult> Handle(
        EditAuthenticationServiceCommand command,
        CancellationToken cancellationToken
    )
    {
        var registration = await repository.FindAsync(command.Id, cancellationToken);
        if (registration is null || registration.PublisherId != command.PublisherId)
        {
            return AuthenticationServiceMutationResult.NotFound;
        }

        if (registration.Revision != command.Revision) { return AuthenticationServiceMutationResult.Conflict; }

        if (await repository.HasUrlAsync(
                command.ServiceUrl.AbsoluteUri,
                command.Id,
                cancellationToken
            ))
        {
            throw new InvalidOperationException("Authentication service URL is already registered.");
        }

        return registration.Edit(command.Name, command.ServiceUrl, command.Description, command.Revision, command.Now)
            ? AuthenticationServiceMutationResult.Completed
            : AuthenticationServiceMutationResult.Conflict;
    }
}
