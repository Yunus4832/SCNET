using ContentServer.Application.Queries;
using ContentServer.Endpoints.Admin.Releases;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Public.Releases;

[HttpGet("/api/v1/game-releases")]
[AllowAnonymous]
public sealed class PublicReleaseListEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var platform = Query<string?>("platform", isRequired: false);
        if (platform is not null &&
            platform is not ("windows-x64" or "linux-x64" or "android-arm64" or "android-arm32"))
        {
            throw new KnownException("invalid_platform", StatusCodes.Status400BadRequest);
        }

        var items = await mediator.Send(new ListGameReleasesQuery(true, platform), cancellationToken);
        await Send.ResponseAsync(items.Select(GameReleaseResponse.From).ToArray().AsResponseData(),
            cancellation: cancellationToken);
    }
}
