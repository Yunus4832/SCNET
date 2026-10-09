using ContentServer.Application.Queries;
using ContentServer.Endpoints.Admin.Contents;
using ContentServer.Endpoints.Public.Contents;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Endpoints.Publisher.Contents;

[HttpGet("/api/v1/publisher/content")]
[AllowAnonymous]
public sealed class PublisherContentEndpoint(IMediator mediator, ApiKeyAuthenticationContext authenticationContext)
    : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var query = Query<string?>("query", isRequired: false);
        var page = ReadPagination();
        var result = await Content(query, page, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<PagedData<ContentItemResponse>>> Content(
        string? query,
        PaginationRequest page,
        CancellationToken cancellationToken)
    {
        var items = await mediator.Send(
            new ListContentItemsQuery(
                page.ToPageRequest(),
                query,
                authenticationContext.RequirePublisherId()),
            cancellationToken);
        return items.Map(item => new ContentItemResponse(
            item.ContentId.ToString(),
            item.PublisherId.ToString(),
            item.Type,
            item.Identifier,
            item.Name,
            item.Summary,
            item.Description,
            item.Status.ToString().ToLowerInvariant(),
            item.CreatedAt,
            item.UpdatedAt)).AsResponseData();
    }
}
