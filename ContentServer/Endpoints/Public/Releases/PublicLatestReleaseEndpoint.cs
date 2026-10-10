using ContentServer.Application.Queries;
using ContentServer.Endpoints.Admin.Releases;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Public.Releases;

[HttpGet("/api/v1/game-releases/latest/{platform}")]
[AllowAnonymous]
public sealed class PublicLatestReleaseEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var platform = Route<string>("platform");
        if (platform is not ("windows-x64" or "linux-x64" or "android-arm64" or "android-arm32"))
        {
            throw new KnownException("invalid_platform", StatusCodes.Status400BadRequest);
        }

        var items = await mediator.Send(new ListGameReleasesQuery(true, platform), cancellationToken);
        var latest = items.FirstOrDefault();
        if (latest is null)
        {
            throw new KnownException("game_release_not_found", StatusCodes.Status404NotFound);
        }

        var artifact = latest.Artifacts.Single(item => item.Platform == platform);
        await Send.ResponseAsync(new LatestGameReleaseResponse(latest.Id.ToString(), latest.Version, artifact.Platform,
            latest.Description, artifact.DownloadUrl, artifact.Sha256,
            latest.Status.ToString().ToLowerInvariant(), latest.CreatedAt, latest.PublishedAt).AsResponseData(),
            cancellation: cancellationToken);
    }
}

public sealed record LatestGameReleaseResponse(
    string Id,
    string Version,
    string Platform,
    string Description,
    string DownloadUrl,
    string Sha256,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt);
