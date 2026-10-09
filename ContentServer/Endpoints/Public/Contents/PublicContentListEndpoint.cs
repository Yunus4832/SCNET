using ContentServer.Application.Queries;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Endpoints.Public.Contents;

[HttpGet("/api/v1/content")]
[AllowAnonymous]
public sealed class PublicContentListEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var type = Query<string?>("type", isRequired: false);
        var query = Query<string?>("query", isRequired: false);
        var page = ReadPagination();
        var result = await Content(type, query, page, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<PagedData<ContentVersionResponse>>> Content(
        string? type,
        string? query,
        PaginationRequest page,
        CancellationToken cancellationToken)
    {
        var items = await mediator.Send(
            new ListVersionsQuery(VersionQueryScope.Public, page.ToPageRequest(), Type: type, Search: query,
                LatestOnly: true),
            cancellationToken
        );
        return items.Map(item => item.ToResponse())
            .AsResponseData();
    }
}

public sealed record ContentVersionResponse(
    string ContentId,
    string PublisherId,
    string Type,
    string Identifier,
    string Name,
    string Summary,
    string Description,
    string ContentStatus,
    string VersionId,
    string Version,
    string PackageHash,
    long PackageSize,
    string FileName,
    string? MetadataJson,
    string Status,
    string? ReviewMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    string DownloadUrl);

public sealed class PaginationRequest
{
    public int PageIndex { get; init; } = 1;

    public int PageSize { get; init; } = 10;

    public PageRequest ToPageRequest()
    {
        return new PageRequest
        {
            PageIndex = PageIndex > 0 ? PageIndex : 1,
            PageSize = Math.Clamp(PageSize > 0 ? PageSize : 10, 1, 100),
            CountTotal = true
        };
    }
}
