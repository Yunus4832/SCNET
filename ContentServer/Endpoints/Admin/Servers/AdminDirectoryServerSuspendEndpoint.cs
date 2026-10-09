using ContentServer.Application.Commands;
using ContentServer.Domain.ServerDirectory;
using ContentServer.Endpoints.Admin.Publishers;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Servers;

[HttpPost("/api/v1/admin/servers/{id:guid}/suspend")]
[AllowAnonymous]
public sealed class AdminDirectoryServerSuspendEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext
) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var id = Route<Guid>("id");
        var request = await ReadBodyAsync<ReviewRequest>(cancellationToken);
        var result = await Suspend(id, request, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private Task<ResponseData> Suspend(Guid id, ReviewRequest request, CancellationToken cancellationToken) =>
        SetSuspended(id, true, request.Message, cancellationToken);

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
