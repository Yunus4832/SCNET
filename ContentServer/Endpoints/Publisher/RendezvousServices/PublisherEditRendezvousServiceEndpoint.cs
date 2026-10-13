using ContentServer.Application;
using ContentServer.Application.Commands;
using ContentServer.Application.Queries;
using ContentServer.Domain.Publishers;
using ContentServer.Domain.RendezvousServices;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Publisher.RendezvousServices;

[HttpPost("/api/v1/publisher/rendezvous-services/{id:guid}/edit")]
[AllowAnonymous]
public sealed class PublisherEditRendezvousServiceEndpoint(IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext, RendezvousInspectionClient inspection) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var publisherId = authenticationContext.RequirePublisherId();
        var publisher = await mediator.Send(new GetPublisherQuery(publisherId), cancellationToken);
        if (publisher?.Status != PublisherStatus.Active)
        {
            throw new KnownException("publisher_not_active", StatusCodes.Status403Forbidden);
        }
        var id = new RendezvousServiceRegistrationId(Route<Guid>("id"));
        var owned = await mediator.Send(new ListRendezvousServicesQuery(Id: id, PublisherId: publisherId), cancellationToken);
        if (owned.Count == 0)
        {
            throw new KnownException("rendezvous_service_not_found", StatusCodes.Status404NotFound);
        }
        var request = await ReadBodyAsync<EditRendezvousServiceRequest>(cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100 || request.Description?.Length > 1000 ||
            !EndpointMappings.TryParsePublicHttpUrl(request.ControlUrl, out var url))
        {
            throw new KnownException("invalid_rendezvous_service_submission", StatusCodes.Status400BadRequest);
        }
        url = new Uri(url.AbsoluteUri.TrimEnd('/') + "/");
        try
        {
            await inspection.InspectAsync(url, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            throw new KnownException("rendezvous_service_unavailable_or_invalid", StatusCodes.Status422UnprocessableEntity);
        }
        try
        {
            if (!await mediator.Send(new EditRendezvousServiceCommand(id, publisherId, request.Name, url, request.Description), cancellationToken))
            {
                throw new KnownException("rendezvous_service_not_found", StatusCodes.Status404NotFound);
            }
        }
        catch (InvalidOperationException)
        {
            throw new KnownException("rendezvous_service_already_submitted", StatusCodes.Status409Conflict);
        }
        await Send.ResponseAsync(new ResponseData(true, "success", StatusCodes.Status200OK), StatusCodes.Status200OK, cancellationToken);
    }
}

public sealed record EditRendezvousServiceRequest(string Name, string ControlUrl, string? Description);
