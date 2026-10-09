using ContentServer.Application.Commands;
using ContentServer.Domain.Administration;
using ContentServer.Endpoints.Admin.Publishers;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Administrators;

[HttpPost("/api/v1/admin/administrator-applications/{administratorId}/reject")]
[AllowAnonymous]
public sealed class AdminRejectAdministratorEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var administratorId = Route<string>("administratorId") ??
                              throw new BadHttpRequestException("Missing route value.");
        var request = await ReadBodyAsync<ReviewRequest>(cancellationToken);
        var result = await RejectAdministrator(administratorId, request, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private Task<ResponseData>
        RejectAdministrator(string administratorId, ReviewRequest request, CancellationToken ct) =>
        ReviewAdministrator(administratorId, AdministratorStatus.Rejected, request.Message, ct);

    private async Task<ResponseData> ReviewAdministrator(string id, AdministratorStatus status, string? message,
        CancellationToken ct)
    {
        var result =
            await mediator.Send(
                new ReviewAdministratorCommand(new AdministratorId(ParseId(id)),
                    authenticationContext.RequireAdministratorId(), status, message), ct);
        if (result == ReviewAdministratorResult.NotFound)
        {
            throw new KnownException("administrator_not_found", 404);
        }

        if (result == ReviewAdministratorResult.InvalidState)
        {
            throw new KnownException("administrator_already_reviewed", 409);
        }

        return Success();
    }

    private static Guid ParseId(string value)
    {
        return Guid.TryParse(value, out var id)
            ? id
            : throw new KnownException("invalid_id", StatusCodes.Status400BadRequest);
    }

    private static ResponseData Success()
    {
        return new ResponseData(true, string.Empty, StatusCodes.Status200OK);
    }
}
