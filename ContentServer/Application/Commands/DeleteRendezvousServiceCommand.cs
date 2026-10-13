using ContentServer.Domain.RendezvousServices;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record DeleteRendezvousServiceCommand(RendezvousServiceRegistrationId Id) : ICommand<bool>;

public sealed class DeleteRendezvousServiceCommandHandler(RendezvousServiceRegistrationRepository repository)
    : ICommandHandler<DeleteRendezvousServiceCommand, bool>
{
    public async Task<bool> Handle(DeleteRendezvousServiceCommand command, CancellationToken cancellationToken)
    {
        var source = await repository.FindAsync(command.Id, cancellationToken);
        if (source is null)
        {
            return false;
        }

        source.Delete(DateTimeOffset.UtcNow);
        return true;
    }
}
