using ContentServer.Application.Commands;
using ContentServer.Application.Queries;
using ContentServer.Domain.Publishers;
using ContentServer.Domain.ServerDirectory;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Publisher.Servers;

[HttpPut("/api/v1/publisher/servers/{id:guid}")]
[AllowAnonymous]
public sealed class PublisherUpdateServerEndpoint(IMediator mediator, ApiKeyAuthenticationContext authenticationContext)
    : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var id = Route<Guid>("id");
        var request = await ReadBodyAsync<UpdateDirectoryServerRequest>(cancellationToken);
        var result = await UpdateServer(id, request, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData> UpdateServer(Guid id, UpdateDirectoryServerRequest request,
        CancellationToken cancellationToken)
    {
        var publisher = await RequirePublisherAsync(cancellationToken);
        if (publisher.Status != PublisherStatus.Active)
        {
            throw new KnownException("publisher_not_active", StatusCodes.Status403Forbidden);
        }

        var normalized = EndpointMappings.ValidateServerUpdate(request);
        var result = await mediator.Send(new UpdateDirectoryServerCommand(new DirectoryServerId(id),
                publisher.PublisherId, request.Name.Trim(), normalized.Address, normalized.Description,
                normalized.Tags),
            cancellationToken);
        EndpointMappings.EnsureServerUpdated(result);
        return new ResponseData(true, "success", StatusCodes.Status200OK);
    }

    private async Task<PublisherDto> RequirePublisherAsync(CancellationToken cancellationToken)
    {
        return await mediator.Send(
            new GetPublisherQuery(authenticationContext.RequirePublisherId()),
            cancellationToken
        ) ?? throw new KnownException("publisher_not_found", StatusCodes.Status401Unauthorized);
    }
}

public sealed record UpdateDirectoryServerRequest(string Name, string Address, string? Description, string[] Tags);
