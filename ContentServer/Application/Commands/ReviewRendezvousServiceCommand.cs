using ContentServer.Domain.Administration;
using ContentServer.Domain.RendezvousServices;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record ReviewRendezvousServiceCommand(
    RendezvousServiceRegistrationId Id,
    AdministratorId AdministratorId,
    bool Approved,
    string? Message
)
    : ICommand<ReviewRendezvousServiceResult>;

public enum ReviewRendezvousServiceResult
{
    Completed = 0,

    NotFound = 1,

    InvalidState = 2
}

public sealed class ReviewRendezvousServiceCommandHandler(RendezvousServiceRegistrationRepository repository)
    : ICommandHandler<ReviewRendezvousServiceCommand, ReviewRendezvousServiceResult>
{
    public async Task<ReviewRendezvousServiceResult> Handle(
        ReviewRendezvousServiceCommand command,
        CancellationToken cancellationToken
    )
    {
        var source = await repository.FindAsync(command.Id, cancellationToken);
        if (source is null)
        {
            return ReviewRendezvousServiceResult.NotFound;
        }

        return source.Review(command.Approved, command.AdministratorId, command.Message, DateTimeOffset.UtcNow)
            ? ReviewRendezvousServiceResult.Completed
            : ReviewRendezvousServiceResult.InvalidState;
    }
}
