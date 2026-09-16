using ContentServer.Domain.Administration;
using ContentServer.Domain.ServerSources;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record ReviewServerSourceCommand(ServerSourceRegistrationId Id, AdministratorId AdministratorId,
    bool Approved, string? Message)
    : ICommand<ReviewServerSourceResult>;

public enum ReviewServerSourceResult
{
    Completed,
    NotFound,
    InvalidState
}

public sealed class ReviewServerSourceCommandHandler(ServerSourceRegistrationRepository repository)
    : ICommandHandler<ReviewServerSourceCommand, ReviewServerSourceResult>
{
    public async Task<ReviewServerSourceResult> Handle(ReviewServerSourceCommand command,
        CancellationToken cancellationToken)
    {
        var source = await repository.FindAsync(command.Id, cancellationToken);
        if (source is null)
        {
            return ReviewServerSourceResult.NotFound;
        }

        return source.Review(command.Approved, command.AdministratorId, command.Message, DateTimeOffset.UtcNow)
            ? ReviewServerSourceResult.Completed
            : ReviewServerSourceResult.InvalidState;
    }
}
