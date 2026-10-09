using ContentServer.Application.Commands;
using ContentServer.Domain.ServerSources;
using ContentServer.Endpoints.Admin.Publishers;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.ServerSources;

[HttpPost("/api/v1/admin/server-sources/{id:guid}/reject")]
[AllowAnonymous]
public sealed class AdminServerSourceRejectEndpoint(IMediator mediator, ApiKeyAuthenticationContext authenticationContext) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var id = Route<Guid>("id");
        var request = await ReadBodyAsync<ReviewRequest>(cancellationToken);
        var result = await Reject(id, request, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData> Reject(Guid id, ReviewRequest request, CancellationToken cancellationToken)
    {
        await Review(id, false, request.Message, cancellationToken);
        return new ResponseData(true, "success", StatusCodes.Status200OK);
    }

    private async Task Review(Guid id, bool approved, string? message, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ReviewServerSourceCommand(new ServerSourceRegistrationId(id),
            authenticationContext.RequireAdministratorId(), approved, message), cancellationToken);
        if (result == ReviewServerSourceResult.NotFound)
        {
            throw new KnownException("server_source_not_found", StatusCodes.Status404NotFound);
        }

        if (result == ReviewServerSourceResult.InvalidState)
        {
            throw new KnownException("server_source_already_reviewed", StatusCodes.Status409Conflict);
        }
    }
}
