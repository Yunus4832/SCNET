using ContentServer.Application.Queries;
using ContentServer.Domain.Announcements;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Endpoints.Admin.Announcements;

[HttpGet("/api/v1/admin/announcements")]
[AllowAnonymous]
public sealed class AdminAnnouncementListEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var items = await mediator.Send(new ListGameAnnouncementsQuery(false), cancellationToken);
        await Send.ResponseAsync(items.Select(AnnouncementResponse.From).ToArray().AsResponseData(),
            cancellation: cancellationToken);
    }
}

public sealed record AnnouncementResponse(
    string Id,
    string Title,
    string Body,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt)
{
    public static AnnouncementResponse From(GameAnnouncement item) => new(item.Id.ToString(), item.Title,
        item.Body, item.Status.ToString().ToLowerInvariant(), item.CreatedAt, item.PublishedAt);
}
