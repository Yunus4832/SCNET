using ContentServer.Application.Commands;
using ContentServer.Domain.RendezvousServices;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.RendezvousServices;

[HttpPost("/api/v1/admin/rendezvous-services/{id:guid}/disable")]
[AllowAnonymous]
public sealed class AdminRendezvousServiceDisableEndpoint(IMediator mediator) : ContentEndpoint
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
                new DisableRendezvousServiceCommand(new RendezvousServiceRegistrationId(id)),
                cancellationToken
            ))
        {
            throw new KnownException("rendezvous_service_not_active", StatusCodes.Status404NotFound);
        }

        return new ResponseData(true, "success", StatusCodes.Status200OK);
    }
}
