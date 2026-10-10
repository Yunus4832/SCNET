using ContentServer.Application.Commands;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Announcements;

[HttpPost("/api/v1/admin/announcements")]
[AllowAnonymous]
public sealed class AdminAnnouncementCreateEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var request = await ReadBodyAsync<CreateAnnouncementRequest>(cancellationToken);
        try
        {
            var id = await mediator.Send(new CreateGameAnnouncementCommand(request.Title, request.Body),
                cancellationToken);
            await Send.ResponseAsync(new { id = id.ToString() }.AsResponseData(), cancellation: cancellationToken);
        }
        catch (ArgumentException)
        {
            throw new KnownException("invalid_announcement", StatusCodes.Status400BadRequest);
        }
    }
}

public sealed record CreateAnnouncementRequest(string Title, string Body);
