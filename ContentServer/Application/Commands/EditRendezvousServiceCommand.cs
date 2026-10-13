using ContentServer.Domain.Publishers;
using ContentServer.Domain.RendezvousServices;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record EditRendezvousServiceCommand(RendezvousServiceRegistrationId Id, PublisherId PublisherId,
    string Name, Uri ControlUrl, string? Description) : ICommand<bool>;

public sealed class EditRendezvousServiceCommandHandler(RendezvousServiceRegistrationRepository repository)
    : ICommandHandler<EditRendezvousServiceCommand, bool>
{
    public async Task<bool> Handle(EditRendezvousServiceCommand command, CancellationToken cancellationToken)
    {
        var registration = await repository.FindAsync(command.Id, cancellationToken);
        if (registration is null || registration.PublisherId != command.PublisherId)
        {
            return false;
        }
        if (await repository.HasPendingOrActiveUrlAsync(command.ControlUrl.AbsoluteUri, cancellationToken, command.Id))
        {
            throw new InvalidOperationException("Rendezvous service URL is already registered.");
        }
        registration.Edit(command.Name, command.ControlUrl, command.Description, DateTimeOffset.UtcNow);
        return true;
    }
}
