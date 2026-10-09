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

[HttpPost("/api/v1/admin/servers/{id:guid}/reject")]
[AllowAnonymous]
public sealed class AdminDirectoryServerRejectEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext
) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var id = Route<Guid>("id");
        var request = await ReadBodyAsync<ReviewRequest>(cancellationToken);
        var result = await Reject(id, request, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private Task<ResponseData> Reject(Guid id, ReviewRequest request, CancellationToken cancellationToken) =>
        Review(id, false, request.Message, cancellationToken);

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
