using ContentServer.Application.Commands;
using ContentServer.Domain.Publishers;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Publishers;

[HttpPost("/api/v1/admin/publishers/{publisherId}/restore-key")]
[AllowAnonymous]
public sealed class AdminRestoreAllPublisherKeysEndpoint(IMediator mediator) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var publisherId = Route<string>("publisherId") ?? throw new BadHttpRequestException("Missing route value.");
        var result = await RestoreAllPublisherKeys(publisherId, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData> RestoreAllPublisherKeys(string publisherId, CancellationToken cancellationToken)
    {
        var found = await mediator.Send(new RestorePublisherKeyCommand(new PublisherId(ParseId(publisherId))),
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
