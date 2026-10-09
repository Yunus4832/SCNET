using ContentServer.Application.Queries;
using ContentServer.Endpoints.Public.ServerSources;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Publisher.ServerSources;

[HttpGet("/api/v1/publisher/server-sources")]
[AllowAnonymous]
public sealed class PublisherServerSourcesEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var result = await ServerSources(cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<ServerSourceRegistrationResponse[]>> ServerSources(
        CancellationToken cancellationToken)
    {
        var publisher = await RequirePublisherAsync(cancellationToken);
        var sources = await mediator.Send(new ListServerSourcesQuery(PublisherId: publisher.PublisherId),
            cancellationToken);
        return sources.Select(EndpointMappings.ServerSource).ToArray().AsResponseData();
    }

    private async Task<PublisherDto> RequirePublisherAsync(CancellationToken cancellationToken)
    {
        return await mediator.Send(
            new GetPublisherQuery(authenticationContext.RequirePublisherId()),
            cancellationToken
        ) ?? throw new KnownException("publisher_not_found", StatusCodes.Status401Unauthorized);
    }
}
