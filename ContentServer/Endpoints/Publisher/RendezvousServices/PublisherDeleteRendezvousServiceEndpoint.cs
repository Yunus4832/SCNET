using ContentServer.Application.Commands;
using ContentServer.Application.Queries;
using ContentServer.Domain.RendezvousServices;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Publisher.RendezvousServices;

[HttpDelete("/api/v1/publisher/rendezvous-services/{id:guid}")]
[AllowAnonymous]
public sealed class PublisherDeleteRendezvousServiceEndpoint(IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var id = new RendezvousServiceRegistrationId(Route<Guid>("id"));
        var items = await mediator.Send(new ListRendezvousServicesQuery(Id: id,
            PublisherId: authenticationContext.RequirePublisherId()), cancellationToken);
        if (items.Count == 0 || !await mediator.Send(new DeleteRendezvousServiceCommand(id), cancellationToken))
        {
            throw new KnownException("rendezvous_service_not_found", StatusCodes.Status404NotFound);
        }
        await Send.ResponseAsync(new ResponseData(true, "success", StatusCodes.Status200OK), StatusCodes.Status200OK, cancellationToken);
    }
}
