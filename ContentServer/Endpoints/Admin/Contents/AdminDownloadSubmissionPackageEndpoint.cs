using ContentServer.Application.Queries;
using ContentServer.Domain.Contents;
using ContentServer.Infrastructure;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Contents;

[HttpGet("/api/v1/admin/submissions/{versionId}/package")]
[AllowAnonymous]
public sealed class AdminDownloadSubmissionPackageEndpoint(
    IMediator mediator,
    ContentPackageStore packageStore
) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var versionId = Route<string>("versionId") ?? throw new BadHttpRequestException("Missing route value.");
        var package = await mediator.Send(
            new DownloadSubmissionPackageQuery(new ContentVersionId(ParseId(versionId))),
            cancellationToken) ?? throw new KnownException(
            "submission_package_not_found",
            StatusCodes.Status404NotFound
        );
        await Results.File(packageStore.Open(package.Hash), package.MediaType, package.FileName,
            enableRangeProcessing: true).ExecuteAsync(HttpContext);
    }

    private static Guid ParseId(string value)
    {
        return Guid.TryParse(value, out var id)
            ? id
            : throw new KnownException("invalid_id", StatusCodes.Status400BadRequest);
    }
}
