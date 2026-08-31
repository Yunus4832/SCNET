using ContentServer.Application.Queries;
using ContentServer.Controllers.Contracts.Requests;
using ContentServer.Controllers.Contracts.Responses;
using ContentServer.Controllers.Mappings;
using ContentServer.Infrastructure;

using MediatR;

using Microsoft.AspNetCore.Mvc;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class PublicController(IMediator mediator, ContentPackageStore packageStore) : ControllerBase
{
    [HttpGet("health")]
    public ResponseData<HealthResponse> Health()
    {
        return new HealthResponse("ContentServer", "v1").AsResponseData();
    }

    [HttpGet("content")]
    public async Task<ResponseData<PagedData<ContentVersionResponse>>> Content(
        [FromQuery] string? type,
        [FromQuery] string? query,
        [FromQuery] PaginationRequest page,
        CancellationToken cancellationToken)
    {
        var items = await mediator.Send(
            new ListVersionsQuery(VersionQueryScope.Public, page.ToPageRequest(), Type: type, Search: query, LatestOnly: true),
            cancellationToken
        );
        return items.Map(item => item.ToResponse())
            .AsResponseData();
    }

    [HttpGet("content/{contentId}")]
    public async Task<ResponseData<ContentVersionResponse>> Content(string contentId, CancellationToken cancellationToken)
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

    [HttpGet("content/{contentId}/versions")]
    public async Task<ResponseData<PagedData<ContentVersionResponse>>> ContentVersions(
        string contentId,
        [FromQuery] PaginationRequest page,
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

    [HttpGet("packages/{hash}")]
    public async Task<IActionResult> Package(string hash, CancellationToken cancellationToken)
    {
        var blob = await mediator.Send(new DownloadPackageQuery(hash), cancellationToken);
        if (blob is null)
        {
            throw new KnownException("package_not_found", StatusCodes.Status404NotFound);
        }

        return File(packageStore.Open(blob.Hash), blob.MediaType, blob.FileName, true);
    }

    [HttpGet("mods")]
    public async Task<ResponseData<PagedData<ModPackageResponse>>> Mods(
        [FromQuery] PaginationRequest page,
        CancellationToken cancellationToken)
    {
        var items = await mediator.Send(
            new ListVersionsQuery(VersionQueryScope.Public, page.ToPageRequest(), Type: "Mod"),
            cancellationToken
        );
        return items.Map(item => item.ToModResponse())
            .AsResponseData();
    }

    [HttpGet("mods/{modId}")]
    public async Task<ResponseData<PagedData<ModPackageResponse>>> Mods(
        string modId,
        [FromQuery] PaginationRequest page,
        CancellationToken cancellationToken)
    {
        var items = await mediator.Send(
            new ListVersionsQuery(
                VersionQueryScope.Public,
                page.ToPageRequest(),
                Type: "Mod",
                Identifier: modId),
            cancellationToken
        );
        if (items.Total == 0)
        {
            throw new KnownException("mod_not_found", StatusCodes.Status404NotFound);
        }

        return items.Map(item => item.ToModResponse())
            .AsResponseData();
    }

    [HttpGet("mods/{modId}/versions/{version}")]
    public async Task<ResponseData<ModPackageResponse>> Mod(
        string modId,
        string version,
        CancellationToken cancellationToken)
    {
        var items = await mediator.Send(
            new ListVersionsQuery(
                VersionQueryScope.Public,
                new PageRequest { PageIndex = 1, PageSize = 1, CountTotal = false },
                Type: "Mod",
                Identifier: modId,
                Version: version),
            cancellationToken
        );
        var item = items.Items.FirstOrDefault();
        if (item is null)
        {
            throw new KnownException("mod_version_not_found", StatusCodes.Status404NotFound);
        }

        return item.ToModResponse().AsResponseData();
    }

    private static Domain.Contents.ContentId ParseContentId(string value)
    {
        return Guid.TryParse(value, out var id)
            ? new Domain.Contents.ContentId(id)
            : throw new KnownException("invalid_id", StatusCodes.Status400BadRequest);
    }
}
