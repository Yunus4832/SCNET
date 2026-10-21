using ContentServer.Application.Commands;
using ContentServer.Domain.RendezvousServices;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.RendezvousServices;

[HttpDelete("/api/v1/admin/rendezvous-services/{id:guid}")]
[AllowAnonymous]
public sealed class AdminRendezvousServiceDeleteEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var id = Route<Guid>("id");
        var result = await Delete(id, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!await mediator.Send(
                new DeleteRendezvousServiceCommand(new RendezvousServiceRegistrationId(id)),
                cancellationToken
            ))
        {
            throw new KnownException("rendezvous_service_not_found", StatusCodes.Status404NotFound);
        }

        return new ResponseData(true, "success", StatusCodes.Status200OK);
    }
}
