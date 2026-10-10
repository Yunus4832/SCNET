using ContentServer.Application.Commands;
using ContentServer.Domain.Announcements;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Announcements;

[HttpPost("/api/v1/admin/announcements/{id:guid}/withdraw")]
[AllowAnonymous]
public sealed class AdminAnnouncementWithdrawEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        try
        {
            var command = new SetGameAnnouncementStatusCommand(new GameAnnouncementId(Route<Guid>("id")), false);
            if (!await mediator.Send(command, cancellationToken))
            {
                throw new KnownException("announcement_not_found", StatusCodes.Status404NotFound);
            }

            await Send.ResponseAsync(new ResponseData(true, "success", StatusCodes.Status200OK),
                cancellation: cancellationToken);
        }
        catch (InvalidOperationException)
        {
            throw new KnownException("invalid_announcement_state", StatusCodes.Status409Conflict);
        }
    }
}
