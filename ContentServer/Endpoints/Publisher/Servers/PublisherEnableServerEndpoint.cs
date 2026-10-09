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

[HttpPost("/api/v1/publisher/servers/{id:guid}/enable")]
[AllowAnonymous]
public sealed class PublisherEnableServerEndpoint(IMediator mediator, ApiKeyAuthenticationContext authenticationContext)
    : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var id = Route<Guid>("id");
        var result = await EnableServer(id, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private Task<ResponseData> EnableServer(Guid id, CancellationToken cancellationToken) =>
        SetServerEnabled(id, true, cancellationToken);

    private async Task<ResponseData> SetServerEnabled(Guid id, bool enabled, CancellationToken cancellationToken)
    {
        var publisher = await RequirePublisherAsync(cancellationToken);
        if (publisher.Status != PublisherStatus.Active)
        {
            throw new KnownException("publisher_not_active", StatusCodes.Status403Forbidden);
        }

        var result = await mediator.Send(new SetPublisherDirectoryServerEnabledCommand(new DirectoryServerId(id),
            publisher.PublisherId, enabled), cancellationToken);
        if (result == SetDirectoryServerStateResult.NotFound)
        {
            throw new KnownException("server_not_found", StatusCodes.Status404NotFound);
        }

        if (result == SetDirectoryServerStateResult.NotOwned)
        {
            throw new KnownException("server_not_owned", StatusCodes.Status403Forbidden);
        }

        return new ResponseData(true, string.Empty, StatusCodes.Status200OK);
    }

    private async Task<PublisherDto> RequirePublisherAsync(CancellationToken cancellationToken)
    {
        return await mediator.Send(
            new GetPublisherQuery(authenticationContext.RequirePublisherId()),
            cancellationToken
        ) ?? throw new KnownException("publisher_not_found", StatusCodes.Status401Unauthorized);
    }
}
