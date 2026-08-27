using ContentServer.Domain.Contents;
using ContentServer.Domain.Packages;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record UpdateContentItemVersionCommand(
    ContentId ContentId,
    string Name,
    string? Summary,
    string Version,
    string? Metadata,
    PackageBlobId PackageBlobId) : ICommand;

public sealed class UpdateContentItemVersionCommandHandler(
    ContentRepository repository
) : ICommandHandler<UpdateContentItemVersionCommand>
{
    public async Task Handle(
        UpdateContentItemVersionCommand command,
        CancellationToken cancellationToken)
    {
        var content = await repository.FindAsync(command.ContentId, cancellationToken)
                      ?? throw new KnownException("content_not_found", 404);
        var now = DateTimeOffset.UtcNow;
        content.UpdateDetails(command.Name, command.Summary, now);
        content.SubmitVersion(
            command.Version,
            command.PackageBlobId,
            command.Metadata,
            now
        );
    }
}
