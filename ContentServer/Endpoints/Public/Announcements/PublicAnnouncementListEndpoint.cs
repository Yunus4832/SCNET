using ContentServer.Application.Queries;
using ContentServer.Endpoints.Admin.Announcements;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Endpoints.Public.Announcements;

[HttpGet("/api/v1/announcements")]
[AllowAnonymous]
public sealed class PublicAnnouncementListEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var items = await mediator.Send(new ListGameAnnouncementsQuery(true), cancellationToken);
        await Send.ResponseAsync(items.Select(AnnouncementResponse.From).ToArray().AsResponseData(),
            cancellation: cancellationToken);
    }
}
