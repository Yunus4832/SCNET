using ContentServer.Application.Commands;
using ContentServer.Domain.ServerDirectory;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Servers;

[HttpDelete("/api/v1/admin/servers/{id:guid}")]
[AllowAnonymous]
public sealed class AdminDirectoryServerDeleteEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var id = Route<Guid>("id");
        var result = await Delete(id, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!await mediator.Send(new DeleteDirectoryServerCommand(new DirectoryServerId(id)), cancellationToken))
        {
            throw new KnownException("server_not_found", StatusCodes.Status404NotFound);
        }

        return new ResponseData(true, "success", StatusCodes.Status200OK);
    }
}
