using ContentServer.Application.Commands;
using ContentServer.Domain.AuthenticationServices;

using MediatR;

using NetCorePal.Extensions.Domain;

namespace ContentServer.Application;

public sealed class AuthenticationServiceChangedHandler(IMediator mediator)
    : IDomainEventHandler<AuthenticationServiceChangedDomainEvent>
{
    public Task Handle(AuthenticationServiceChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.AdministratorId is not { } administratorId) { return Task.CompletedTask; }

        return mediator.Send(
            new CreateReviewRecordCommand(
                administratorId,
                "AuthenticationService",
                domainEvent.Registration.Id.ToString(),
                domainEvent.Action,
                domainEvent.Message,
                domainEvent.OccurredAt
            ),
            cancellationToken
        );
    }
}
