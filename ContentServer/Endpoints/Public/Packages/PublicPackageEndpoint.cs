using ContentServer.Application.Queries;
using ContentServer.Infrastructure;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Public.Packages;

[HttpGet("/api/v1/packages/{hash}")]
[AllowAnonymous]
public sealed class PublicPackageEndpoint(IMediator mediator, ContentPackageStore packageStore) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var hash = Route<string>("hash") ?? throw new BadHttpRequestException("Missing route value.");
        var blob = await mediator.Send(new DownloadPackageQuery(hash), cancellationToken);
        if (blob is null)
        {
            throw new KnownException("package_not_found", StatusCodes.Status404NotFound);
        }

        await Results.File(packageStore.Open(blob.Hash), blob.MediaType, blob.FileName,
            enableRangeProcessing: true).ExecuteAsync(HttpContext);
    }
}
