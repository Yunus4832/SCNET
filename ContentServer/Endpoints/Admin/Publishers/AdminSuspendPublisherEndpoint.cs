using ContentServer.Application.Commands;
using ContentServer.Domain.Publishers;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Publishers;

[HttpPost("/api/v1/admin/publishers/{publisherId}/suspend")]
[AllowAnonymous]
public sealed class AdminSuspendPublisherEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext
) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var publisherId = Route<string>("publisherId") ?? throw new BadHttpRequestException("Missing route value.");
        var review = await ReadBodyAsync<ReviewRequest>(cancellationToken);
        var result = await SuspendPublisher(publisherId, review, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private Task<ResponseData> SuspendPublisher(
        string publisherId,
        ReviewRequest review,
        CancellationToken cancellationToken
    )
    {
        return SuspendPublisherCore(publisherId, review.Message, cancellationToken);
    }

    private async Task<ResponseData> SuspendPublisherCore(
        string id,
        string? message,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SuspendPublisherCommand(
            new PublisherId(ParseId(id)),
            authenticationContext.RequireAdministratorId(),
            message), cancellationToken);
        EnsureReviewCompleted(result, "publisher_not_found", "publisher_cannot_be_suspended");
        return Success();
    }

    private static Guid ParseId(string value)
    {
        return Guid.TryParse(value, out var id)
            ? id
            : throw new KnownException("invalid_id", StatusCodes.Status400BadRequest);
    }

    private static void EnsureReviewCompleted(
        ReviewPublisherResult result,
        string notFoundMessage,
        string invalidStateMessage)
    {
        if (result == ReviewPublisherResult.NotFound)
        {
            throw new KnownException(notFoundMessage, StatusCodes.Status404NotFound);
        }

        if (result == ReviewPublisherResult.InvalidState)
        {
            throw new KnownException(invalidStateMessage, StatusCodes.Status409Conflict);
        }
    }

    private static ResponseData Success()
    {
        return new ResponseData(true, string.Empty, StatusCodes.Status200OK);
    }
}
