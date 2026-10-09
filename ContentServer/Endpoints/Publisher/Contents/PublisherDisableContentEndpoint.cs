using ContentServer.Application.Commands;
using ContentServer.Application.Queries;
using ContentServer.Domain.Contents;
using ContentServer.Domain.Publishers;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Publisher.Contents;

[HttpPost("/api/v1/publisher/content/{contentId}/disable")]
[AllowAnonymous]
public sealed class PublisherDisableContentEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var contentId = Route<string>("contentId") ?? throw new BadHttpRequestException("Missing route value.");
        var result = await DisableContent(contentId, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private Task<ResponseData> DisableContent(string contentId, CancellationToken cancellationToken) =>
        SetContentStatus(contentId, ContentStatus.Disabled, cancellationToken);

    private async Task<ResponseData> SetContentStatus(
        string contentId,
        ContentStatus status,
        CancellationToken cancellationToken)
    {
        var publisher = await RequirePublisherAsync(cancellationToken);
        if (publisher.Status != PublisherStatus.Active)
        {
            throw new KnownException("publisher_not_active", StatusCodes.Status403Forbidden);
        }

        if (!Guid.TryParse(contentId, out var id))
        {
            throw new KnownException("invalid_id", StatusCodes.Status400BadRequest);
        }

        var result = await mediator.Send(
            new SetPublisherContentStatusCommand(
                new ContentId(id),
                publisher.PublisherId,
                status),
            cancellationToken);
        if (result == SetPublisherContentStatusResult.NotFound)
        {
            throw new KnownException("content_not_found", StatusCodes.Status404NotFound);
        }

        if (result == SetPublisherContentStatusResult.NotOwned)
        {
            throw new KnownException("content_not_owned", StatusCodes.Status403Forbidden);
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
