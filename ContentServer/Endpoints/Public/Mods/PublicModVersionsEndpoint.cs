using ContentServer.Application.Queries;
using ContentServer.Endpoints.Public.Contents;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Public.Mods;

[HttpGet("/api/v1/mods/{modId}")]
[AllowAnonymous]
public sealed class PublicModVersionsEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var modId = Route<string>("modId") ?? throw new BadHttpRequestException("Missing route value.");
        var page = ReadPagination();
        var result = await Mods(modId, page, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<PagedData<ModPackageResponse>>> Mods(
        string modId,
        PaginationRequest page,
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
}
