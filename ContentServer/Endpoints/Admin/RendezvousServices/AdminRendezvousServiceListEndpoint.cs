using ContentServer.Application.Queries;
using ContentServer.Domain.RendezvousServices;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;

namespace ContentServer.Endpoints.Admin.RendezvousServices;

[HttpGet("/api/v1/admin/rendezvous-services")]
[AllowAnonymous]
public sealed class AdminRendezvousServiceListEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var status = Query<RendezvousServiceRegistrationStatus?>("status", isRequired: false);
        var items = await mediator.Send(new ListRendezvousServicesQuery(status), cancellationToken);
        await Send.ResponseAsync(items.Select(EndpointMappings.RendezvousService).ToArray().AsResponseData(),
            StatusCodes.Status200OK, cancellationToken);
    }
}
