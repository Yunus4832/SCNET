using ContentServer.Application.Queries;
using ContentServer.Endpoints.Public.Contents;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Endpoints.Public.Mods;

[HttpGet("/api/v1/mods")]
[AllowAnonymous]
public sealed class PublicModsListEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var page = ReadPagination();
        var result = await Mods(page, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<PagedData<ModPackageResponse>>> Mods(
        PaginationRequest page,
        CancellationToken cancellationToken)
    {
        var items = await mediator.Send(
            new ListVersionsQuery(VersionQueryScope.Public, page.ToPageRequest(), Type: "Mod"),
            cancellationToken
        );
        return items.Map(item => item.ToModResponse())
            .AsResponseData();
    }
}

public sealed record ModPackageResponse(
    string ModId,
    string Version,
    string PackageHash,
    string FileName,
    long PackageSize,
    string Side,
    string Summary,
    string Description,
    DateTimeOffset UploadedAtUtc,
    string DownloadUrl);
