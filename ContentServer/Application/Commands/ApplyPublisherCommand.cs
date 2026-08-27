using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;
using ContentServer.Utils;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record ApplyPublisherCommand(
    string DisplayName,
    string Contact,
    string? Description
) : ICommand<PublisherApplicationDto>;

public sealed record PublisherApplicationDto(
    PublisherId PublisherId,
    PublisherStatus Status,
    string ApiKey);

public sealed class ApplyPublisherCommandHandler(
    PublisherRepository repository
) : ICommandHandler<ApplyPublisherCommand, PublisherApplicationDto>
{
    public async Task<PublisherApplicationDto> Handle(
        ApplyPublisherCommand command,
        CancellationToken cancellationToken)
    {
        var key = ApiKeyUtility.GeneratePublisherKey();
        var publisher = Publisher.Apply(
            command.DisplayName,
            command.Contact,
            command.Description,
            ApiKeyUtility.GetPrefix(key),
            ApiKeyUtility.Hash(key),
            DateTimeOffset.UtcNow
        );
        await repository.AddAsync(publisher, cancellationToken);
        return new PublisherApplicationDto(publisher.Id, publisher.Status, key);
    }
}
