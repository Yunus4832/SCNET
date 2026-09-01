using ContentServer.Domain.Administration;
using ContentServer.Domain.Contents;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record ReviewContentVersionCommand(
    ContentVersionId VersionId,
    AdministratorId AdministratorId,
    ContentVersionStatus Status,
    string? Message
) : ICommand<ReviewContentVersionResult>;

public enum ReviewContentVersionResult
{
    Completed,
    NotFound,
    InvalidState
}

public sealed class ReviewContentVersionCommandHandler(ContentRepository repository)
    : ICommandHandler<ReviewContentVersionCommand, ReviewContentVersionResult>
{
    public async Task<ReviewContentVersionResult> Handle(ReviewContentVersionCommand command,
        CancellationToken cancellationToken)
    {
        var content = await repository.FindByVersionAsync(command.VersionId, cancellationToken);
        if (content is null)
        {
            return ReviewContentVersionResult.NotFound;
        }

        return content.ReviewVersion(
            command.VersionId,
            command.AdministratorId,
            command.Status,
            command.Message,
            DateTimeOffset.UtcNow
        )
            ? ReviewContentVersionResult.Completed
            : ReviewContentVersionResult.InvalidState;
    }
}
