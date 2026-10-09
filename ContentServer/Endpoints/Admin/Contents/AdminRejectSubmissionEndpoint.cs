using ContentServer.Application.Commands;
using ContentServer.Domain.Contents;
using ContentServer.Endpoints.Admin.Publishers;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Contents;

[HttpPost("/api/v1/admin/submissions/{versionId}/reject")]
[AllowAnonymous]
public sealed class AdminRejectSubmissionEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext
) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var versionId = Route<string>("versionId") ?? throw new BadHttpRequestException("Missing route value.");
        var review = await ReadBodyAsync<ReviewRequest>(cancellationToken);
        var result = await RejectSubmission(versionId, review, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private Task<ResponseData> RejectSubmission(
        string versionId,
        ReviewRequest review,
        CancellationToken cancellationToken
    )
    {
        return ReviewVersion(versionId, ContentVersionStatus.Rejected, review.Message, cancellationToken);
    }

    private async Task<ResponseData> ReviewVersion(
        string id,
        ContentVersionStatus status,
        string? message,
        CancellationToken cancellationToken
    )
    {
        var result = await mediator.Send(
            new ReviewContentVersionCommand(
                new ContentVersionId(ParseId(id)),
                authenticationContext.RequireAdministratorId(),
                status,
                message
            ),
            cancellationToken
        );
        if (result == ReviewContentVersionResult.NotFound)
        {
            throw new KnownException("submission_not_found", StatusCodes.Status404NotFound);
        }

        if (result == ReviewContentVersionResult.InvalidState)
        {
            throw new KnownException("submission_already_reviewed", StatusCodes.Status409Conflict);
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
