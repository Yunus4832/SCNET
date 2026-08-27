using ContentServer.Domain.Administration;
using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record RevokePublisherKeyCommand(
    PublisherId PublisherId,
    AdministratorId AdministratorId
) : ICommand<bool>;

public sealed class RevokePublisherKeyCommandHandler(PublisherRepository repository)
    : ICommandHandler<RevokePublisherKeyCommand, bool>
{
    public async Task<bool> Handle(
        RevokePublisherKeyCommand command,
        CancellationToken cancellationToken
    )
    {
        var publisher = await repository.FindWithKeysAsync(command.PublisherId, cancellationToken);
        return publisher is not null && publisher.RevokeKeys(command.AdministratorId, DateTimeOffset.UtcNow);
    }
}

public sealed record RestorePublisherKeyCommand(PublisherId PublisherId) : ICommand<bool>;

public sealed class RestorePublisherKeyCommandHandler(
    PublisherRepository repository
) : ICommandHandler<RestorePublisherKeyCommand, bool>
{
    public async Task<bool> Handle(RestorePublisherKeyCommand command, CancellationToken cancellationToken)
    {
        var publisher = await repository.FindWithKeysAsync(command.PublisherId, cancellationToken);
        return publisher is not null && publisher.RestoreKeys(DateTimeOffset.UtcNow);
    }
}
