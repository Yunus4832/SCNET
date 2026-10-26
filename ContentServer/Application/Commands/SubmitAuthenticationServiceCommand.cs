using ContentServer.Domain.AuthenticationServices;
using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record SubmitAuthenticationServiceCommand(
    PublisherId PublisherId,
    string Name,
    Uri ServiceUrl,
    string? Description,
    DateTimeOffset Now
)
    : ICommand<AuthenticationServiceRegistrationId>;

public sealed class SubmitAuthenticationServiceCommandHandler(AuthenticationServiceRegistrationRepository repository)
    : ICommandHandler<SubmitAuthenticationServiceCommand, AuthenticationServiceRegistrationId>
{
    public async Task<AuthenticationServiceRegistrationId> Handle(
        SubmitAuthenticationServiceCommand command,
        CancellationToken cancellationToken
    )
    {
        if (await repository.HasUrlAsync(
                command.ServiceUrl.AbsoluteUri,
                null,
                cancellationToken
            ))
        {
            throw new InvalidOperationException("Authentication service URL is already registered.");
        }

        var registration = AuthenticationServiceRegistration.Submit(
            command.PublisherId,
            command.Name,
            command.ServiceUrl,
            command.Description,
            command.Now
        );
        await repository.AddAsync(registration, cancellationToken);
        return registration.Id;
    }
}
