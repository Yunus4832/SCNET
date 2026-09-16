using ContentServer.Domain.Administration;
using ContentServer.Domain.ServerDirectory;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public enum ReviewDirectoryServerResult
{
    Success,
    NotFound,
    InvalidState
}

public sealed record ReviewDirectoryServerCommand(DirectoryServerId Id, AdministratorId AdministratorId,
    bool Approved, string? Message) : ICommand<ReviewDirectoryServerResult>;

public sealed class ReviewDirectoryServerCommandHandler(DirectoryServerRepository repository)
    : ICommandHandler<ReviewDirectoryServerCommand, ReviewDirectoryServerResult>
{
    public async Task<ReviewDirectoryServerResult> Handle(ReviewDirectoryServerCommand command,
        CancellationToken cancellationToken)
    {
        var server = await repository.FindAsync(command.Id, cancellationToken);
        if (server is null)
        {
            return ReviewDirectoryServerResult.NotFound;
        }

        return server.Review(command.Approved, command.AdministratorId, command.Message, DateTimeOffset.UtcNow)
            ? ReviewDirectoryServerResult.Success
            : ReviewDirectoryServerResult.InvalidState;
    }
}
