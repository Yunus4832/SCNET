using ContentServer.Domain.Publishers;
using ContentServer.Domain.RendezvousServices;
using ContentServer.Infrastructure;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Application.Commands;

public sealed record SubmitRendezvousServiceCommand(PublisherId PublisherId, string Name, Uri ControlUrl, string? Description)
    : ICommand<SubmitRendezvousServiceResult>;

public sealed record SubmitRendezvousServiceResult(RendezvousServiceRegistrationId Id,
    RendezvousServiceRegistrationStatus Status, DateTimeOffset CreatedAt);

public sealed class SubmitRendezvousServiceCommandHandler(RendezvousServiceRegistrationRepository repository)
    : ICommandHandler<SubmitRendezvousServiceCommand, SubmitRendezvousServiceResult>
{
    public async Task<SubmitRendezvousServiceResult> Handle(SubmitRendezvousServiceCommand command,
        CancellationToken cancellationToken)
    {
        if (await repository.HasPendingOrActiveUrlAsync(command.ControlUrl.AbsoluteUri, cancellationToken))
        {
            throw new InvalidOperationException("Rendezvous service URL is already registered.");
        }

        var source = RendezvousServiceRegistration.Submit(command.PublisherId, command.Name, command.ControlUrl,
            command.Description, DateTimeOffset.UtcNow);
        await repository.AddAsync(source, cancellationToken);
        return new SubmitRendezvousServiceResult(source.Id, source.Status, source.CreatedAt);
    }
}
