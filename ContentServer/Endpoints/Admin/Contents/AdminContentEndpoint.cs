using ContentServer.Application.Queries;
using ContentServer.Endpoints.Public.Contents;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Endpoints.Admin.Contents;

[HttpGet("/api/v1/admin/content")]
[AllowAnonymous]
public sealed class AdminContentEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var query = Query<string?>("query", isRequired: false);
        var type = Query<string?>("type", isRequired: false);
        var page = ReadPagination();
        var result = await Content(query, type, page, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<PagedData<ContentItemResponse>>> Content(
        string? query,
        string? type,
        PaginationRequest page,
        CancellationToken cancellationToken)
    {
        var items = await mediator.Send(
            new ListContentItemsQuery(page.ToPageRequest(), query, Type: type),
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
            item.UpdatedAt)
        ).AsResponseData();
    }


}

public sealed record ContentItemResponse(
    string ContentId,
    string PublisherId,
    string Type,
    string Identifier,
    string Name,
    string Summary,
    string Description,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
