using ContentServer.Application.Queries;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Public.Contents;

[HttpGet("/api/v1/content/{contentId}")]
[AllowAnonymous]
public sealed class PublicContentDetailsEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var contentId = Route<string>("contentId") ?? throw new BadHttpRequestException("Missing route value.");
        var result = await Content(contentId, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<ContentVersionResponse>> Content(string contentId,
        CancellationToken cancellationToken)
    {
        var id = ParseContentId(contentId);
        var items = await mediator.Send(new ListVersionsQuery(
            VersionQueryScope.Public,
            new PageRequest { PageIndex = 1, PageSize = 1, CountTotal = false },
            ContentId: id,
            LatestOnly: true), cancellationToken);
        var item = items.Items.FirstOrDefault()
                   ?? throw new KnownException("content_not_found", StatusCodes.Status404NotFound);
        return item.ToResponse().AsResponseData();
    }

    private static Domain.Contents.ContentId ParseContentId(string value)
    {
        return Guid.TryParse(value, out var id)
            ? new Domain.Contents.ContentId(id)
            : throw new KnownException("invalid_id", StatusCodes.Status400BadRequest);
    }
}
