using ContentServer.Application.Commands;
using ContentServer.Domain.Releases;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Releases;

[HttpPost("/api/v1/admin/game-releases/{id:guid}/withdraw")]
[AllowAnonymous]
public sealed class AdminReleaseWithdrawEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        try
        {
            var command = new SetGameReleaseStatusCommand(new GameReleaseId(Route<Guid>("id")), false);
            if (!await mediator.Send(command, cancellationToken))
            {
                throw new KnownException("game_release_not_found", StatusCodes.Status404NotFound);
            }

            await Send.ResponseAsync(new ResponseData(true, "success", StatusCodes.Status200OK),
                cancellation: cancellationToken);
        }
        catch (InvalidOperationException)
        {
            throw new KnownException("invalid_game_release_state", StatusCodes.Status409Conflict);
        }
    }
}
