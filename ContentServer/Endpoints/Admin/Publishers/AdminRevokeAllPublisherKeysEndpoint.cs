using ContentServer.Application.Commands;
using ContentServer.Domain.Publishers;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Publishers;

[HttpPost("/api/v1/admin/publishers/{publisherId}/revoke-key")]
[AllowAnonymous]
public sealed class AdminRevokeAllPublisherKeysEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext
) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var publisherId = Route<string>("publisherId") ?? throw new BadHttpRequestException("Missing route value.");
        var result = await RevokeAllPublisherKeys(publisherId, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData> RevokeAllPublisherKeys(string publisherId, CancellationToken cancellationToken)
    {
        var found = await mediator.Send(new RevokePublisherKeyCommand(
                new PublisherId(ParseId(publisherId)), authenticationContext.RequireAdministratorId()),
            cancellationToken);
        EnsureFound(found, "publisher_or_key_not_found");
        return Success();
    }

    private static Guid ParseId(string value)
    {
        return Guid.TryParse(value, out var id)
            ? id
            : throw new KnownException("invalid_id", StatusCodes.Status400BadRequest);
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
