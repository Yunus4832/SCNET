using ContentServer.Application.Queries;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Public.RendezvousServices;

[HttpGet("/api/v1/rendezvous-services")]
[AllowAnonymous]
public sealed class RendezvousServiceListEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var page = ReadPagination().ToPageRequest();
        if (page.PageIndex > 100)
        {
            throw new KnownException("invalid_pagination", StatusCodes.Status400BadRequest);
        }
        var services = await mediator.Send(new ListPublicRendezvousServicesQuery(page), cancellationToken);
        await Send.ResponseAsync(services.Map(service => new RendezvousServiceResponse(service.Id.ToString(),
            service.Name, service.ControlUrl, service.Description)).AsResponseData(), StatusCodes.Status200OK, cancellationToken);
    }
}

public sealed record RendezvousServiceResponse(string Id, string Name, string ControlUrl, string? Description);

public sealed record RendezvousServiceRegistrationResponse(
    string Id,
    string PublisherId,
    string PublisherName,
    string Name,
    string ControlUrl,
    string? Description,
    string Status,
    string? ReviewMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt);
