using ContentServer.Domain.RendezvousServices;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record DisableRendezvousServiceCommand(RendezvousServiceRegistrationId Id) : ICommand<bool>;

public sealed class DisableRendezvousServiceCommandHandler(RendezvousServiceRegistrationRepository repository)
    : ICommandHandler<DisableRendezvousServiceCommand, bool>
{
    public async Task<bool> Handle(DisableRendezvousServiceCommand command, CancellationToken cancellationToken)
    {
        var registration = await repository.FindAsync(command.Id, cancellationToken);
        return registration?.Disable(DateTimeOffset.UtcNow) == true;
    }
}
