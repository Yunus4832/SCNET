using ContentServer.Application.Commands;
using ContentServer.Application.Queries;
using ContentServer.Controllers.Contracts.Requests;
using ContentServer.Controllers.Contracts.Responses;
using ContentServer.Controllers.Mappings;
using ContentServer.Domain.Administration;
using ContentServer.Domain.Contents;
using ContentServer.Domain.Publishers;
using ContentServer.Infrastructure;
using ContentServer.Middlewares;

using MediatR;

using Microsoft.AspNetCore.Mvc;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Controllers;

[ApiController]
[Route("api/v1/admin")]
public sealed class AdminController(
    IMediator mediator,
    ApiKeyAuthenticationContext authenticationContext,
    ContentPackageStore packageStore
) : ControllerBase
{
    [HttpGet("administrator-applications")]
    public async Task<ResponseData<PagedData<AdministratorResponse>>> AdministratorApplications(
        [FromQuery] AdministratorStatus? status, [FromQuery] string? query,
        [FromQuery] PaginationRequest page, CancellationToken ct)
    {
        var items = await mediator.Send(new ListAdministratorsQuery(page.ToPageRequest(), status, Search: query), ct);
        return items.Map(AdministratorApplicationController.ToResponse).AsResponseData();
    }

    [HttpPost("administrator-applications/{administratorId}/approve")]
    public Task<ResponseData> ApproveAdministrator(string administratorId, CancellationToken ct) =>
        ReviewAdministrator(administratorId, AdministratorStatus.Active, null, ct);

    [HttpPost("administrator-applications/{administratorId}/reject")]
    public Task<ResponseData>
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

    [HttpGet("self")]
    public ResponseData<AdministratorSelfResponse> Self()
    {
        return new AdministratorSelfResponse(
            authenticationContext.RequireAdministratorId().ToString(),
            "active").AsResponseData();
    }

    [HttpGet("publishers")]
    public async Task<ResponseData<PagedData<PublisherResponse>>> Publishers(
        [FromQuery] PublisherStatus? status,
        [FromQuery] string? query,
        [FromQuery] PaginationRequest page,
        CancellationToken cancellationToken)
    {
        var items = await mediator.Send(
            new ListPublishersQuery(page.ToPageRequest(), status, query),
            cancellationToken);
        return items.Map(item => item.ToResponse())
            .AsResponseData();
    }

    [HttpPost("publishers/{publisherId}/approve")]
    public Task<ResponseData> ApprovePublisher(string publisherId, CancellationToken cancellationToken)
    {
        return ReviewPublisher(publisherId, PublisherStatus.Active, null, cancellationToken);
    }

    [HttpPost("publishers/{publisherId}/reject")]
    public Task<ResponseData> RejectPublisher(
        string publisherId,
        ReviewRequest review,
        CancellationToken cancellationToken
    )
    {
        return ReviewPublisher(publisherId, PublisherStatus.Rejected, review.Message, cancellationToken);
    }

    [HttpPost("publishers/{publisherId}/suspend")]
    public Task<ResponseData> SuspendPublisher(
        string publisherId,
        ReviewRequest review,
        CancellationToken cancellationToken
    )
    {
        return SuspendPublisherCore(publisherId, review.Message, cancellationToken);
    }

    [HttpPost("publishers/{publisherId}/revoke-key")]
    public async Task<ResponseData> RevokeAllPublisherKeys(string publisherId, CancellationToken cancellationToken)
    {
        var found = await mediator.Send(new RevokePublisherKeyCommand(
                new PublisherId(ParseId(publisherId)), authenticationContext.RequireAdministratorId()),
            cancellationToken);
        EnsureFound(found, "publisher_or_key_not_found");
        return Success();
    }

    [HttpPost("publishers/{publisherId}/restore-key")]
    public async Task<ResponseData> RestoreAllPublisherKeys(string publisherId, CancellationToken cancellationToken)
    {
        var found = await mediator.Send(new RestorePublisherKeyCommand(new PublisherId(ParseId(publisherId))),
            cancellationToken);
        EnsureFound(found, "publisher_or_key_not_found");
        return Success();
    }

    [HttpPost("administrators/{administratorId}/revoke-key")]
    public async Task<ResponseData> RevokeAdministratorKeys(string administratorId, CancellationToken cancellationToken)
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

    [HttpPost("administrators/{administratorId}/restore-key")]
    public async Task<ResponseData> RestoreAdministratorKeys(string administratorId,
        CancellationToken cancellationToken)
    {
        if (!await IsSuperAdministratorAsync(cancellationToken))
        {
            throw new KnownException("super_administrator_required", StatusCodes.Status403Forbidden);
        }

        var found = await mediator.Send(new RestoreAdministratorKeyCommand(
            new AdministratorId(ParseId(administratorId))), cancellationToken);
        EnsureFound(found, "administrator_key_not_found_or_protected");
        return Success();
    }

    [HttpGet("submissions")]
    public async Task<ResponseData<PagedData<ContentVersionResponse>>> Submissions(
        [FromQuery] ContentVersionStatus? status,
        [FromQuery] string? query,
        [FromQuery] PaginationRequest page,
        CancellationToken cancellationToken)
    {
        var items = await mediator.Send(
            new ListVersionsQuery(VersionQueryScope.All, page.ToPageRequest(), Search: query, Status: status),
            cancellationToken);
        return items.Map(item => item.ToResponse())
            .AsResponseData();
    }

    [HttpGet("content")]
    public async Task<ResponseData<PagedData<ContentItemResponse>>> Content(
        [FromQuery] string? query,
        [FromQuery] string? type,
        [FromQuery] PaginationRequest page,
        CancellationToken cancellationToken)
    {
        var items = await mediator.Send(
            new ListContentItemsQuery(page.ToPageRequest(), query, Type: type),
            cancellationToken);
        return items.Map(item => new ContentItemResponse(
            item.ContentId.ToString(),
            item.PublisherId.ToString(),
            item.Type,
            item.Identifier,
            item.Name,
            item.Summary,
            item.Status.ToString().ToLowerInvariant(),
            item.CreatedAt,
            item.UpdatedAt)
        ).AsResponseData();
    }

    [HttpPost("submissions/{versionId}/approve")]
    public Task<ResponseData> ApproveSubmission(string versionId, CancellationToken cancellationToken)
    {
        return ReviewVersion(versionId, ContentVersionStatus.Published, null, cancellationToken);
    }

    [HttpPost("submissions/{versionId}/reject")]
    public Task<ResponseData> RejectSubmission(
        string versionId,
        ReviewRequest review,
        CancellationToken cancellationToken
    )
    {
        return ReviewVersion(versionId, ContentVersionStatus.Rejected, review.Message, cancellationToken);
    }

    [HttpPost("content/{contentId}/disable")]
    public Task<ResponseData> DisableContent(
        string contentId,
        CancellationToken cancellationToken
    )
    {
        return SetContentStatus(contentId, ContentStatus.Disabled, cancellationToken);
    }

    [HttpPost("content/{contentId}/enable")]
    public Task<ResponseData> EnableContent(
        string contentId,
        CancellationToken cancellationToken
    )
    {
        return SetContentStatus(contentId, ContentStatus.Active, cancellationToken);
    }

    private async Task<ResponseData> ReviewPublisher(
        string id,
        PublisherStatus status,
        string? message,
        CancellationToken cancellationToken
    )
    {
        var result = await mediator.Send(
            new ReviewPublisherCommand(
                new PublisherId(ParseId(id)),
                authenticationContext.RequireAdministratorId(),
                status,
                message
            ),
            cancellationToken
        );
        EnsureReviewCompleted(result, "publisher_not_found", "publisher_already_reviewed");
        return Success();
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

    [HttpGet("submissions/{versionId}/package")]
    public async Task<IActionResult> DownloadSubmissionPackage(
        string versionId,
        CancellationToken cancellationToken)
    {
        var package = await mediator.Send(
            new DownloadSubmissionPackageQuery(new ContentVersionId(ParseId(versionId))),
            cancellationToken) ?? throw new KnownException(
            "submission_package_not_found",
            StatusCodes.Status404NotFound
        );
        return File(packageStore.Open(package.Hash), package.MediaType, package.FileName, true);
    }

    private static ResponseData Success()
    {
        return new ResponseData(true, string.Empty, StatusCodes.Status200OK, null);
    }
}
