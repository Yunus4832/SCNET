using ContentServer.Application.Commands;
using ContentServer.Domain.Releases;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Releases;

[HttpPut("/api/v1/admin/game-releases/{id:guid}")]
[AllowAnonymous]
public sealed class AdminReleaseUpdateEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var request = await ReadBodyAsync<SaveGameReleaseRequest>(cancellationToken);
        try
        {
            ArgumentNullException.ThrowIfNull(request.Artifacts);
            var artifacts = request.Artifacts.Select(artifact => new GameReleaseArtifactData(
                artifact.Platform, artifact.DownloadUrl, artifact.Sha256)).ToArray();
            var updated = await mediator.Send(new EditGameReleaseCommand(new GameReleaseId(Route<Guid>("id")),
                request.Version, request.Description, artifacts), cancellationToken);
            if (!updated)
            {
                throw new KnownException("game_release_not_found", StatusCodes.Status404NotFound);
            }

            await Send.ResponseAsync(new ResponseData(true, "success", StatusCodes.Status200OK),
                cancellation: cancellationToken);
        }
        catch (ArgumentException)
        {
            throw new KnownException("invalid_game_release", StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException)
        {
            throw new KnownException("invalid_game_release_state", StatusCodes.Status409Conflict);
        }
    }

}
