using ContentServer.Application.Commands;
using ContentServer.Domain.ServerSources;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.ServerSources;

[HttpDelete("/api/v1/admin/server-sources/{id:guid}")]
[AllowAnonymous]
public sealed class AdminServerSourceDeleteEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var id = Route<Guid>("id");
        var result = await Delete(id, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!await mediator.Send(new DeleteServerSourceCommand(new ServerSourceRegistrationId(id)),
                cancellationToken))
        {
            throw new KnownException("server_source_not_found", StatusCodes.Status404NotFound);
        }
        return new ResponseData(true, "success", StatusCodes.Status200OK);
    }


}
