using ContentServer.Application.Queries;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Public.Contents;

[HttpGet("/api/v1/content/{contentId}/versions")]
[AllowAnonymous]
public sealed class PublicContentVersionsEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var contentId = Route<string>("contentId") ?? throw new BadHttpRequestException("Missing route value.");
        var page = ReadPagination();
        var result = await ContentVersions(contentId, page, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<PagedData<ContentVersionResponse>>> ContentVersions(
        string contentId,
        PaginationRequest page,
        CancellationToken cancellationToken)
    {
        var items = await mediator.Send(new ListVersionsQuery(
            VersionQueryScope.Public,
            page.ToPageRequest(),
            ContentId: ParseContentId(contentId)), cancellationToken);
        if (items.Total == 0)
        {
            throw new KnownException("content_not_found", StatusCodes.Status404NotFound);
        }

        return items.Map(item => item.ToResponse()).AsResponseData();
    }

    private static Domain.Contents.ContentId ParseContentId(string value)
    {
        return Guid.TryParse(value, out var id)
            ? new Domain.Contents.ContentId(id)
            : throw new KnownException("invalid_id", StatusCodes.Status400BadRequest);
    }
}
