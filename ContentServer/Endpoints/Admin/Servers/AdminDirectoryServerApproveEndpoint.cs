using ContentServer.Application.Commands;
using ContentServer.Domain.ServerDirectory;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Servers;

[HttpPost("/api/v1/admin/servers/{id:guid}/approve")]
[AllowAnonymous]
public sealed class AdminDirectoryServerApproveEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext
) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var id = Route<Guid>("id");
        var result = await Approve(id, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private Task<ResponseData> Approve(Guid id, CancellationToken cancellationToken) =>
        Review(id, true, null, cancellationToken);

    private async Task<ResponseData> Review(Guid id, bool approved, string? message,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ReviewDirectoryServerCommand(new DirectoryServerId(id),
            authenticationContext.RequireAdministratorId(), approved, message), cancellationToken);
        if (result == ReviewDirectoryServerResult.NotFound)
        {
            throw new KnownException("server_not_found", StatusCodes.Status404NotFound);
        }

        if (result == ReviewDirectoryServerResult.InvalidState)
        {
            throw new KnownException("server_already_reviewed", StatusCodes.Status409Conflict);
        }

        return new ResponseData(true, "success", StatusCodes.Status200OK);
    }
}
