using ContentServer.Application.Queries;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Publisher.Servers;

[HttpGet("/api/v1/publisher/servers")]
[AllowAnonymous]
public sealed class PublisherServersEndpoint(IMediator mediator, ApiKeyAuthenticationContext authenticationContext)
    : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var result = await Servers(cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<DirectoryServerResponse[]>> Servers(CancellationToken cancellationToken)
    {
        var publisher = await RequirePublisherAsync(cancellationToken);
        var servers = await mediator.Send(new ListDirectoryServersQuery(PublisherId: publisher.PublisherId),
            cancellationToken);
        return servers.Select(EndpointMappings.DirectoryServer).ToArray().AsResponseData();
    }

    private async Task<PublisherDto> RequirePublisherAsync(CancellationToken cancellationToken)
    {
        return await mediator.Send(
            new GetPublisherQuery(authenticationContext.RequirePublisherId()),
            cancellationToken
        ) ?? throw new KnownException("publisher_not_found", StatusCodes.Status401Unauthorized);
    }
}

public sealed record DirectoryServerResponse(
    string Id,
    string PublisherId,
    string PublisherName,
    string Name,
    string Address,
    string? Description,
    string[] Tags,
    string ReviewStatus,
    string? ReviewMessage,
    bool IsEnabledByPublisher,
    bool IsSuspended,
    string? SuspensionReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ReviewedAt);
