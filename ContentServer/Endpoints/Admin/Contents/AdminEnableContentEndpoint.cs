using ContentServer.Application.Commands;
using ContentServer.Domain.Contents;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Contents;

[HttpPost("/api/v1/admin/content/{contentId}/enable")]
[AllowAnonymous]
public sealed class AdminEnableContentEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext
) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var contentId = Route<string>("contentId") ?? throw new BadHttpRequestException("Missing route value.");
        var result = await EnableContent(contentId, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private Task<ResponseData> EnableContent(
        string contentId,
        CancellationToken cancellationToken
    )
    {
        return SetContentStatus(contentId, ContentStatus.Active, cancellationToken);
    }

    private async Task<ResponseData> SetContentStatus(
        string id,
        ContentStatus status,
        CancellationToken cancellationToken
    )
    {
        var found = await mediator.Send(
            new SetContentStatusCommand(
                new ContentId(ParseId(id)),
                authenticationContext.RequireAdministratorId(),
                status
            ),
            cancellationToken
        );
        EnsureFound(found, "content_not_found");
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
