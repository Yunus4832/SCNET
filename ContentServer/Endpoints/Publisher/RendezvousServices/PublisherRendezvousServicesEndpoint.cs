using ContentServer.Application.Queries;
using ContentServer.Endpoints.Public.RendezvousServices;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Publisher.RendezvousServices;

[HttpGet("/api/v1/publisher/rendezvous-services")]
[AllowAnonymous]
public sealed class PublisherRendezvousServicesEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext
) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var result = await RendezvousServices(cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<RendezvousServiceRegistrationResponse[]>> RendezvousServices(
        CancellationToken cancellationToken
    )
    {
        var publisher = await RequirePublisherAsync(cancellationToken);
        var sources = await mediator.Send(
            new ListRendezvousServicesQuery(PublisherId: publisher.PublisherId),
            cancellationToken
        );
        return sources.Select(EndpointMappings.RendezvousService).ToArray().AsResponseData();
    }

    private async Task<PublisherDto> RequirePublisherAsync(CancellationToken cancellationToken)
    {
        return await mediator.Send(
                   new GetPublisherQuery(authenticationContext.RequirePublisherId()),
                   cancellationToken
               ) ??
               throw new KnownException("publisher_not_found", StatusCodes.Status401Unauthorized);
    }
}
