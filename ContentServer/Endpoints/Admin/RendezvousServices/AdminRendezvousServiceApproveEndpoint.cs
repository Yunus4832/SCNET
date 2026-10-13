using ContentServer.Application;
using ContentServer.Application.Commands;
using ContentServer.Application.Queries;
using ContentServer.Domain.RendezvousServices;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.RendezvousServices;

[HttpPost("/api/v1/admin/rendezvous-services/{id:guid}/approve")]
[AllowAnonymous]
public sealed class AdminRendezvousServiceApproveEndpoint(
    IMediator mediator,
    RendezvousInspectionClient inspection,
    ApiKeyAuthenticationContext authenticationContext) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var id = Route<Guid>("id");
        var result = await Approve(id, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData> Approve(Guid id, CancellationToken cancellationToken)
    {
        var source = await Find(id, cancellationToken);
        try
        {
            await inspection.InspectAsync(new Uri(source.ControlUrl), cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or
                                              System.Text.Json.JsonException)
        {
            throw new KnownException("rendezvous_service_unavailable_or_invalid", StatusCodes.Status422UnprocessableEntity);
        }

        await Review(id, true, null, cancellationToken);
        return new ResponseData(true, "success", StatusCodes.Status200OK);
    }

    private async Task<RendezvousServiceDto> Find(Guid id, CancellationToken cancellationToken)
    {
        var sources = await mediator.Send(new ListRendezvousServicesQuery(Id: new RendezvousServiceRegistrationId(id)),
            cancellationToken);
        return sources.FirstOrDefault()
               ?? throw new KnownException("rendezvous_service_not_found", StatusCodes.Status404NotFound);
    }

    private async Task Review(Guid id, bool approved, string? message, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ReviewRendezvousServiceCommand(new RendezvousServiceRegistrationId(id),
            authenticationContext.RequireAdministratorId(), approved, message), cancellationToken);
        if (result == ReviewRendezvousServiceResult.NotFound)
        {
            throw new KnownException("rendezvous_service_not_found", StatusCodes.Status404NotFound);
        }

        if (result == ReviewRendezvousServiceResult.InvalidState)
        {
            throw new KnownException("rendezvous_service_already_reviewed", StatusCodes.Status409Conflict);
        }
    }
}
