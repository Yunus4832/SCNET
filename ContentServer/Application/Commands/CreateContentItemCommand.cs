using ContentServer.Domain.Contents;
using ContentServer.Domain.Packages;
using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record CreateContentItemCommand(
    PublisherId PublisherId,
    string Type,
    string Identifier,
    string Name,
    string? Summary,
    string Version,
    string? Metadata,
    PackageBlobId PackageBlobId) : ICommand;

public sealed class CreateContentItemCommandHandler(
    ContentRepository repository
) : ICommandHandler<CreateContentItemCommand>
{
    public async Task Handle(
        CreateContentItemCommand command,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var content = ContentItem.Create(
            command.PublisherId,
            command.Type,
            command.Identifier,
            command.Name,
            command.Summary,
            now);
        content.SubmitVersion(
            command.Version,
            command.PackageBlobId,
            command.Metadata,
            now);

        await repository.AddAsync(content, cancellationToken);
    }
}
