using ContentServer.Application.Commands;
using ContentServer.Domain.Administration;
using ContentServer.Domain.Contents;
using ContentServer.Domain.Publishers;

using MediatR;

using NetCorePal.Extensions.Domain;

namespace ContentServer.Application;

public sealed class AdministratorStatusChangedHandler(IMediator mediator)
    : IDomainEventHandler<AdministratorStatusChangedDomainEvent>
{
    public Task Handle(AdministratorStatusChangedDomainEvent e, CancellationToken ct) => mediator.Send(
        new CreateReviewRecordCommand(e.ReviewerId, "Administrator", e.Administrator.Id.ToString(), e.Status.ToString(),
            e.Message, e.OccurredAt), ct);
}

public sealed class PublisherAppliedHandler(
    ILogger<PublisherAppliedHandler> logger
) : IDomainEventHandler<PublisherAppliedDomainEvent>
{
    public Task Handle(PublisherAppliedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation("Publisher {PublisherId} applied", domainEvent.Publisher.Id);
        return Task.CompletedTask;
    }
}

public sealed class PublisherStatusChangedHandler(
    IMediator mediator,
    ILogger<PublisherStatusChangedHandler> logger
) : IDomainEventHandler<PublisherStatusChangedDomainEvent>
{
    public async Task Handle(PublisherStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await mediator.Send(
            new CreateReviewRecordCommand(
                domainEvent.AdministratorId,
                "Publisher",
                domainEvent.Publisher.Id.ToString(),
                domainEvent.Status.ToString(),
                domainEvent.Message,
                domainEvent.OccurredAt
            ),
            cancellationToken
        );
        logger.LogInformation(
            "Publisher {PublisherId} changed to {Status}",
            domainEvent.Publisher.Id,
            domainEvent.Status
        );
    }
}

public sealed class PublisherKeysRevokedHandler(
    IMediator mediator
) : IDomainEventHandler<PublisherKeysRevokedDomainEvent>
{
    public async Task Handle(PublisherKeysRevokedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await mediator.Send(
            new CreateReviewRecordCommand(
                domainEvent.AdministratorId,
                "Publisher",
                domainEvent.Publisher.Id.ToString(),
                "RevokeKey",
                null,
                domainEvent.OccurredAt
            ),
            cancellationToken
        );
    }
}

public sealed class ContentVersionSubmittedHandler(
    ILogger<ContentVersionSubmittedHandler> logger
) : IDomainEventHandler<ContentVersionSubmittedDomainEvent>
{
    public Task Handle(ContentVersionSubmittedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation("Content version {VersionId} submitted", domainEvent.Version.Id);
        return Task.CompletedTask;
    }
}

public sealed class ContentVersionReviewedHandler(
    IMediator mediator
) : IDomainEventHandler<ContentVersionReviewedDomainEvent>
{
    public async Task Handle(ContentVersionReviewedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await mediator.Send(
            new CreateReviewRecordCommand(
                domainEvent.AdministratorId,
                "ContentVersion",
                domainEvent.Version.Id.ToString(),
                domainEvent.Status.ToString(),
                domainEvent.Message,
                domainEvent.OccurredAt
            ),
            cancellationToken
        );
    }
}

public sealed class ContentStatusChangedHandler(
    IMediator mediator
) : IDomainEventHandler<ContentStatusChangedDomainEvent>
{
    public async Task Handle(ContentStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await mediator.Send(
            new CreateReviewRecordCommand(
                domainEvent.AdministratorId,
                "Content",
                domainEvent.Content.Id.ToString(),
                domainEvent.Status.ToString(),
                null,
                domainEvent.OccurredAt
            ),
            cancellationToken
        );
    }
}
