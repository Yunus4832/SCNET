using ContentServer.Application.Commands;
using ContentServer.Application.Queries;
using ContentServer.Domain.Administration;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Administrators;

[HttpPost("/api/v1/admin/administrators/{administratorId}/revoke-key")]
[AllowAnonymous]
public sealed class AdminRevokeAdministratorKeysEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var administratorId = Route<string>("administratorId") ??
                              throw new BadHttpRequestException("Missing route value.");
        var result = await RevokeAdministratorKeys(administratorId, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData> RevokeAdministratorKeys(string administratorId,
        CancellationToken cancellationToken)
    {
        if (!await IsSuperAdministratorAsync(cancellationToken))
        {
            throw new KnownException("super_administrator_required", StatusCodes.Status403Forbidden);
        }

        var found = await mediator.Send(new RevokeAdministratorKeyCommand(
            new AdministratorId(ParseId(administratorId))), cancellationToken);
        EnsureFound(found, "administrator_key_not_found_or_protected");
        return Success();
    }

    private static Guid ParseId(string value)
    {
        return Guid.TryParse(value, out var id)
            ? id
            : throw new KnownException("invalid_id", StatusCodes.Status400BadRequest);
    }

    private async Task<bool> IsSuperAdministratorAsync(CancellationToken cancellationToken)
    {
        var administratorId = authenticationContext.RequireAdministratorId();
        var page = await mediator.Send(new ListAdministratorsQuery(
            new PageRequest { PageIndex = 1, PageSize = 1, CountTotal = false },
            AdministratorId: administratorId), cancellationToken);
        return page.Items.FirstOrDefault()?.IsSuperAdministrator == true;
    }

    private static void EnsureFound(bool found, string message)
    {
        if (!found)
        {
            throw new KnownException(message, StatusCodes.Status404NotFound);
        }
    }

    private static ResponseData Success()
    {
        return new ResponseData(true, string.Empty, StatusCodes.Status200OK);
    }
}
