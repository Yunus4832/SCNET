using ContentServer.Application.Queries;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Public.Mods;

[HttpGet("/api/v1/mods/{modId}/versions/{version}")]
[AllowAnonymous]
public sealed class PublicModEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var modId = Route<string>("modId") ?? throw new BadHttpRequestException("Missing route value.");
        var version = Route<string>("version") ?? throw new BadHttpRequestException("Missing route value.");
        var result = await Mod(modId, version, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<ModPackageResponse>> Mod(
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
}
