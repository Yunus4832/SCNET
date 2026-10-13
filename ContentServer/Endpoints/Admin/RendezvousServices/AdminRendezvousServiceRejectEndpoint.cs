using ContentServer.Application.Commands;
using ContentServer.Domain.RendezvousServices;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.RendezvousServices;

[HttpPost("/api/v1/admin/rendezvous-services/{id:guid}/reject")]
[AllowAnonymous]
public sealed class AdminRendezvousServiceRejectEndpoint(IMediator mediator, ApiKeyAuthenticationContext authenticationContext) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var id = Route<Guid>("id");
        var request = await ReadBodyAsync<RendezvousServiceReviewRequest>(cancellationToken);
        var result = await Reject(id, request, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData> Reject(Guid id, RendezvousServiceReviewRequest request, CancellationToken cancellationToken)
    {
        await Review(id, false, request.Message, cancellationToken);
        return new ResponseData(true, "success", StatusCodes.Status200OK);
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

public sealed record RendezvousServiceReviewRequest(string? Message);
