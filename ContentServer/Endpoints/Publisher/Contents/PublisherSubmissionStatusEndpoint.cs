using ContentServer.Application.Queries;
using ContentServer.Domain.Contents;
using ContentServer.Endpoints.Public.Contents;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Publisher.Contents;

[HttpGet("/api/v1/publisher/submissions/{versionId}")]
[AllowAnonymous]
public sealed class PublisherSubmissionStatusEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext) : ContentEndpoint
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var versionId = Route<string>("versionId") ?? throw new BadHttpRequestException("Missing route value.");
        var result = await SubmissionStatus(versionId, cancellationToken);
        await Send.ResponseAsync(result, HttpContext.Response.StatusCode, cancellationToken);
    }

    private async Task<ResponseData<ContentVersionResponse>> SubmissionStatus(
        string versionId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(versionId, out var id))
        {
            throw new KnownException("invalid_id", StatusCodes.Status400BadRequest);
        }

        var item = await mediator.Send(new GetPublisherSubmissionQuery(
                       authenticationContext.RequirePublisherId(),
                       new ContentVersionId(id)), cancellationToken)
                   ?? throw new KnownException("submission_not_found", StatusCodes.Status404NotFound);
        return item.ToResponse().AsResponseData();
    }
}
