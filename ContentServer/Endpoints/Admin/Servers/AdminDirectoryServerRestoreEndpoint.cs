using ContentServer.Application.Commands;
using ContentServer.Domain.ServerDirectory;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Servers;

[HttpPost("/api/v1/admin/servers/{id:guid}/restore")]
[AllowAnonymous]
public sealed class AdminDirectoryServerRestoreEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext
) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var id = Route<Guid>("id");
        var result = await Restore(id, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private Task<ResponseData> Restore(Guid id, CancellationToken cancellationToken) =>
        SetSuspended(id, false, null, cancellationToken);

    private async Task<ResponseData> SetSuspended(Guid id, bool suspended, string? reason,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SetDirectoryServerSuspendedCommand(new DirectoryServerId(id),
            authenticationContext.RequireAdministratorId(), suspended, reason), cancellationToken);
        if (result == SetDirectoryServerStateResult.NotFound)
        {
            throw new KnownException("server_not_found", StatusCodes.Status404NotFound);
        }

        if (result == SetDirectoryServerStateResult.InvalidState)
        {
            throw new KnownException("server_state_unchanged", StatusCodes.Status409Conflict);
        }

        return new ResponseData(true, "success", StatusCodes.Status200OK);
    }
}
