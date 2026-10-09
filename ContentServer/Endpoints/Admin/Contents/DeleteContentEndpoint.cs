using ContentServer.Application.Commands;
using ContentServer.Application.Queries;
using ContentServer.Domain.Contents;
using ContentServer.Endpoints;
using ContentServer.Infrastructure;
using ContentServer.Middlewares;

using FastEndpoints;

using MediatR;

using Microsoft.AspNetCore.Authorization;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Endpoints.Admin.Contents;

[HttpDelete("/api/v1/admin/content/{contentId}")]
[AllowAnonymous]
public sealed class DeleteContentEndpoint(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext,
    ContentSubmissionLock submissionLock,
    ContentPackageStore packageStore) : EndpointWithoutRequest
{
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        _ = authenticationContext.RequireAdministratorId();
        var value = Route<string>("contentId");
        if (!Guid.TryParse(value, out var id))
        {
            throw new KnownException("invalid_id", StatusCodes.Status400BadRequest);
        }

        using var lease = await submissionLock.EnterAsync(cancellationToken);
        if (!await mediator.Send(new DeleteContentCommand(new ContentId(id)), cancellationToken))
        {
            throw new KnownException("content_not_found", StatusCodes.Status404NotFound);
        }

        var deletedHashes = await mediator.Send(new ListDeletedPackageHashesQuery(), cancellationToken);
        foreach (var hash in deletedHashes)
        {
            packageStore.Delete(hash);
        }

        await Send.OkAsync(new ResponseData(true, string.Empty, StatusCodes.Status200OK), cancellationToken);
    }
}
