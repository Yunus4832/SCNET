using ContentServer.Application.Commands;
using ContentServer.Domain.Releases;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Releases;

[HttpPost("/api/v1/admin/game-releases")]
[AllowAnonymous]
public sealed class AdminReleaseCreateEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var request = await ReadBodyAsync<SaveGameReleaseRequest>(cancellationToken);
        try
        {
            ArgumentNullException.ThrowIfNull(request.Artifacts);
            var artifacts = request.Artifacts.Select(artifact => new GameReleaseArtifactData(
                artifact.Platform, artifact.DownloadUrl, artifact.Sha256)).ToArray();
            var id = await mediator.Send(new CreateGameReleaseCommand(request.Version, request.Description,
                artifacts), cancellationToken);
            await Send.ResponseAsync(new { id = id.ToString() }.AsResponseData(), cancellation: cancellationToken);
        }
        catch (ArgumentException)
        {
            throw new KnownException("invalid_game_release", StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException)
        {
            throw new KnownException("game_release_exists", StatusCodes.Status409Conflict);
        }
    }

}

public sealed record SaveGameReleaseRequest(
    string Version,
    string Description,
    IReadOnlyList<SaveGameReleaseArtifactRequest> Artifacts);

public sealed record SaveGameReleaseArtifactRequest(
    string Platform,
    string DownloadUrl,
    string Sha256);
