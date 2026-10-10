using ContentServer.Application.Queries;
using ContentServer.Domain.Releases;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Endpoints.Admin.Releases;

[HttpGet("/api/v1/admin/game-releases")]
[AllowAnonymous]
public sealed class AdminReleaseListEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var items = await mediator.Send(new ListGameReleasesQuery(false), cancellationToken);
        await Send.ResponseAsync(items.Select(GameReleaseResponse.From).ToArray().AsResponseData(),
            cancellation: cancellationToken);
    }
}

public sealed record GameReleaseResponse(
    string Id,
    string Version,
    string Description,
    IReadOnlyList<GameReleaseArtifactResponse> Artifacts,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt)
{
    public static GameReleaseResponse From(GameRelease item) => new(item.Id.ToString(), item.Version,
        item.Description, item.Artifacts.Select(GameReleaseArtifactResponse.From).ToArray(),
        item.Status.ToString().ToLowerInvariant(), item.CreatedAt, item.PublishedAt);
}

public sealed record GameReleaseArtifactResponse(
    string Platform,
    string DownloadUrl,
    string Sha256)
{
    public static GameReleaseArtifactResponse From(GameReleaseArtifact item) =>
        new(item.Platform, item.DownloadUrl, item.Sha256);
}
