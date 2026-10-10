using ContentServer.Application.Commands;
using ContentServer.Domain.Announcements;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Announcements;

[HttpPut("/api/v1/admin/announcements/{id:guid}")]
[AllowAnonymous]
public sealed class AdminAnnouncementUpdateEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var request = await ReadBodyAsync<CreateAnnouncementRequest>(cancellationToken);
        try
        {
            var updated = await mediator.Send(new EditGameAnnouncementCommand(
                new GameAnnouncementId(Route<Guid>("id")), request.Title, request.Body), cancellationToken);
            if (!updated)
            {
                throw new KnownException("announcement_not_found", StatusCodes.Status404NotFound);
            }

            await Send.ResponseAsync(new ResponseData(true, "success", StatusCodes.Status200OK),
                cancellation: cancellationToken);
        }
        catch (ArgumentException)
        {
            throw new KnownException("invalid_announcement", StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException)
        {
            throw new KnownException("announcement_not_draft", StatusCodes.Status409Conflict);
        }
    }
}
