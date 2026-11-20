using ContentServer.Domain.Administration;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public enum ReviewAdministratorResult
{
    Completed = 0,

    NotFound = 1,

    InvalidState = 2
}

public sealed record ReviewAdministratorCommand(
    AdministratorId AdministratorId,
    AdministratorId ReviewerId,
    AdministratorStatus Status,
    string? Message
) : ICommand<ReviewAdministratorResult>;

public sealed class ReviewAdministratorCommandHandler(AdministratorRepository repository)
    : ICommandHandler<ReviewAdministratorCommand, ReviewAdministratorResult>
{
    public async Task<ReviewAdministratorResult> Handle(
        ReviewAdministratorCommand command,
        CancellationToken cancellationToken
    )
    {
        var item = await repository.FindAsync(command.AdministratorId, cancellationToken);
        if (item is null)
        {
            return ReviewAdministratorResult.NotFound;
        }

        return item.Review(command.Status, command.ReviewerId, command.Message, DateTimeOffset.UtcNow)
            ? ReviewAdministratorResult.Completed
            : ReviewAdministratorResult.InvalidState;
    }
}
